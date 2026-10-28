using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Content.Shared.Antag;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Roles.Components;
using Robust.Server.Console;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Network.Messages;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Server._ClawCommand.Administration;

/// <summary>
/// View variables and debug-console monitoring:
/// <list type="bullet">
/// <item>Every admin's VV activity (field edits, path writes/invokes, addcomp/rmcomp, and plain views) is
/// recorded, whether or not they're a critical player. It's batched per admin into one plain-English
/// admin-log line every <see cref="VvBatchDelay"/>, instead of one entry per click.</item>
/// <item>Critical players additionally get at most one short admin-chat notice per <see cref="ThrottleInterval"/>,
/// pointing at the admin log for detail.</item>
/// <item>Any admin looking at antag data through VV (minds, mind roles, antag body components) counts as an
/// antag reveal, and toolshed queries touching that data get a throttled notice.</item>
/// </list>
/// The engine has no events for VV traffic, so the server's existing VV net-message handlers are wrapped once
/// at startup. If the engine internals ever change, this logs a warning and only the console hooks remain.
/// </summary>
public sealed partial class CriticalPlayerAlertSystem
{
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IConGroupController _conGroup = default!;
    [Dependency] private IServerConsoleHost _console = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IViewVariablesManager _vvManager = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a given admin's VV edits/views are collected before they're written as one admin-log line.
    /// </summary>
    private static readonly TimeSpan VvBatchDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How many distinct changes or viewed things are listed before the rest is summarized as "+N more".
    /// </summary>
    private const int MaxListedVv = 8;

    private static readonly char[] CommandSeparators = [' ', '\t', ',', '(', ')', '[', ']', '{', '}', '|', ';', '"'];

    /// <summary>
    /// Components antag specifiers add that ordinary crew can have too, so seeing them reveals nothing.
    /// </summary>
    private static readonly HashSet<string> GenericAntagComponents =
    [
        "AntagImmune",
        "AutoImplant",
        "BibleUser",
        "DeathAcidifierImplant",
        "NpcFactionMember",
        "Pacified",
        "RandomMetadata",
        "Thieving",
        "ZombieImmune",
    ];

    /// <summary>
    /// Antag body components added from code rather than through an antag specifier.
    /// </summary>
    private static readonly string[] ExtraSensitiveComponents =
    [
        "BlobCarrier",
        "BloodCultist",
        "ChangelingIdentity",
        "Devil",
        "Ghoul",
        "Heretic",
    ];

    private readonly HashSet<string> _sensitiveNames = new();
    private readonly HashSet<Type> _sensitiveTypes = new();

    private readonly Dictionary<(NetUserId User, string Channel), ThrottledAlert> _throttled = new();

    /// <summary>
    /// What each VV session the server opened points at, for readable edit logs.
    /// </summary>
    private readonly Dictionary<uint, string> _vvSessions = new();

    /// <summary>
    /// Every admin's VV changes and views this round, collected until <see cref="VvBatchDelay"/> passes
    /// with no new activity from them, then written as one log line and cleared.
    /// </summary>
    private readonly Dictionary<NetUserId, PendingVv> _pendingVv = new();
    private readonly List<NetUserId> _toFlushVv = new();

    private readonly List<(object Data, FieldInfo Field, ProcessMessage Original)> _wrapped = new();
    private FieldInfo? _nextSessionIdField;
    private uint _sessionIdBeforeRequest;

    private void InitializeViewVariables()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        RebuildSensitiveComponents();

        _console.AnyCommandExecuted += OnAnyCommandExecuted;

        // Integration tests use their own net manager; there is nothing to wrap there.
        if (_net is not NetManager net)
            return;

        _nextSessionIdField = _vvManager.GetType().GetField("_nextSessionId", BindingFlags.Instance | BindingFlags.NonPublic);

        WrapNetMessage(net, nameof(MsgViewVariablesReqSession), BeforeReqSession, AfterReqSession);
        WrapNetMessage(net, nameof(MsgViewVariablesModifyRemote), OnModifyRemote);
        WrapNetMessage(net, "MsgViewVariablesReadPathReq", OnReadPath);
        WrapNetMessage(net, "MsgViewVariablesWritePathReq", OnWritePath);
        WrapNetMessage(net, "MsgViewVariablesInvokePathReq", OnInvokePath);
        WrapNetMessage(net, nameof(MsgConCmd), OnConCmd);
    }

    private void ShutdownViewVariables()
    {
        _console.AnyCommandExecuted -= OnAnyCommandExecuted;

        foreach (var (data, field, original) in _wrapped)
        {
            field.SetValue(data, original);
        }

        _wrapped.Clear();
    }

    private void WrapNetMessage(NetManager net,
        string name,
        Action<ICommonSession, NetMessage> before,
        Action<ICommonSession, NetMessage>? after = null)
    {
        var dataField = typeof(NetManager).GetField("_messages", BindingFlags.Instance | BindingFlags.NonPublic);
        if (dataField?.GetValue(net) is not IDictionary messages
            || !messages.Contains(name)
            || messages[name] is not { } data
            || data.GetType().GetField("Callback") is not { } callbackField
            || callbackField.GetValue(data) is not ProcessMessage original)
        {
            Log.Warning($"Could not hook {name}; VV activity through it will not be reported.");
            return;
        }

        ProcessMessage wrapped = msg =>
        {
            _player.TryGetSessionByChannel(msg.MsgChannel, out var session);

            if (session != null)
                RunHook(before, session, msg);

            original(msg);

            if (session != null && after != null)
                RunHook(after, session, msg);
        };

        callbackField.SetValue(data, wrapped);
        _wrapped.Add((data, callbackField, original));
    }

    private void RunHook(Action<ICommonSession, NetMessage> hook, ICommonSession session, NetMessage msg)
    {
        try
        {
            hook(session, msg);
        }
        catch (Exception e)
        {
            Log.Error($"Error while reporting {msg.MsgName} from {session}: {e}");
        }
    }

    #region VV window

    private void BeforeReqSession(ICommonSession session, NetMessage msg)
    {
        _sessionIdBeforeRequest = _nextSessionIdField?.GetValue(_vvManager) is uint id ? id : 0;
    }

    private void AfterReqSession(ICommonSession session, NetMessage msg)
    {
        if (msg is not MsgViewVariablesReqSession req
            || _nextSessionIdField?.GetValue(_vvManager) is not uint next
            || next == _sessionIdBeforeRequest)
        {
            // Denied, so nothing was shown.
            return;
        }

        string description;
        switch (req.Selector)
        {
            case ViewVariablesComponentSelector comp:
                // The selector carries the full type name, e.g. Content.Shared.Mind.MindComponent.
                var compName = comp.ComponentType[(comp.ComponentType.LastIndexOf('.') + 1)..];
                if (compName.EndsWith("Component"))
                    compName = compName[..^"Component".Length];

                description = $"{ToPrettyString(GetEntity(comp.Entity))} {compName}";
                CheckSensitiveView(session, GetEntity(comp.Entity), compName);
                break;
            case ViewVariablesEntitySelector ent:
                description = ToPrettyString(GetEntity(ent.Entity)).ToString();
                CheckSensitiveView(session, GetEntity(ent.Entity), null);
                break;
            case ViewVariablesSessionRelativeSelector relative:
                description = _vvSessions.GetValueOrDefault(relative.SessionId) ?? $"VV session {relative.SessionId}";
                break;
            case ViewVariablesPathSelector path:
                description = path.Path;
                CheckSensitivePath(session, path.Path);
                break;
            default:
                description = req.Selector.GetType().Name;
                break;
        }

        _vvSessions[_sessionIdBeforeRequest] = description;
        ReportVvView(session, description);
    }

    private void OnModifyRemote(ICommonSession session, NetMessage msg)
    {
        if (msg is not MsgViewVariablesModifyRemote modify)
            return;

        var target = _vvSessions.GetValueOrDefault(modify.SessionId) ?? $"VV session {modify.SessionId}";
        ReportCriticalVV(session, "edit", $"{target} = {modify.Value}");
    }

    #endregion

    #region VV paths

    private void OnReadPath(ICommonSession session, NetMessage msg)
    {
        if (GetPath(msg) is not { } path || !_conGroup.CanCommand(session, "vvread"))
            return;

        CheckSensitivePath(session, path);
        ReportVvView(session, path);
    }

    private void OnWritePath(ICommonSession session, NetMessage msg)
    {
        if (GetPath(msg) is not { } path || !_conGroup.CanCommand(session, "vvwrite"))
            return;

        CheckSensitivePath(session, path);
        ReportCriticalVV(session, "edit", $"{path} = {GetValue(msg)}");
    }

    private void OnInvokePath(ICommonSession session, NetMessage msg)
    {
        if (GetPath(msg) is not { } path || !_conGroup.CanCommand(session, "vvinvoke"))
            return;

        ReportCriticalVV(session, "invoke", $"{path} {GetValue(msg)}");
    }

    // The path messages are engine-internal types, so their fields are read by name.
    private static string? GetPath(NetMessage msg)
    {
        return msg.GetType().GetProperty("Path")?.GetValue(msg) as string;
    }

    private static string? GetValue(NetMessage msg)
    {
        return msg.GetType().GetProperty("Value")?.GetValue(msg) as string;
    }

    #endregion

    #region Console

    private void OnAnyCommandExecuted(IConsoleShell shell, string command, string argStr, string[] args)
    {
        if (shell.Player is not { } session || args.Length == 0)
            return;

        switch (command)
        {
            case "addcomp":
                ReportCriticalVV(session, "added", string.Join(' ', args));
                break;
            case "rmcomp":
                ReportCriticalVV(session, "removed", string.Join(' ', args));
                break;
            case "vvwrite":
                CheckSensitivePath(session, args[0]);
                ReportCriticalVV(session, "edit", string.Join(' ', args));
                break;
            case "vvinvoke":
                ReportCriticalVV(session, "invoke", string.Join(' ', args));
                break;
            case "vvread":
                CheckSensitivePath(session, args[0]);
                ReportVvView(session, args[0]);
                break;
        }
    }

    /// <summary>
    /// Toolshed commands never reach <see cref="IConsoleHost.AnyCommandExecuted"/>, so raw console input is
    /// checked for queries that touch minds, roles or antag components.
    /// </summary>
    private void OnConCmd(ICommonSession session, NetMessage msg)
    {
        if (msg is not MsgConCmd { Text: { } text } || !_adminManager.IsAdmin(session))
            return;

        var tokens = text.Split(CommandSeparators, StringSplitOptions.RemoveEmptyEntries);

        // Registered commands are handled in OnAnyCommandExecuted.
        if (tokens.Length == 0 || _console.AvailableCommands.ContainsKey(tokens[0]))
            return;

        if (!text.Contains("mind:", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("roles", StringComparison.OrdinalIgnoreCase)
            && !tokens.Any(_sensitiveNames.Contains))
        {
            return;
        }

        _adminLog.Add(LogType.AntagReveal, LogImpact.High,
            $"{session:player} ran a toolshed query touching antag data: {text}");

        Throttle(session, "query", TryDescribe(session, out var who) ? who : session.Name, "query");
    }

    #endregion

    #region Reporting

    /// <summary>
    /// Queues a VV change for every admin's batched log line; critical players additionally get a
    /// throttled admin-chat notice (their role status stays masked in that notice).
    /// </summary>
    private void ReportCriticalVV(ICommonSession session, string kind, string detail)
    {
        ReportVv(session, kind, detail);

        if (TryDescribe(session, out var who))
            Throttle(session, "vv", who, kind);
    }

    /// <summary>
    /// Records one VV change for <paramref name="session"/>, batched into their next log line.
    /// </summary>
    private void ReportVv(ICommonSession session, string kind, string detail)
    {
        if (GetPendingVv(session) is not { } pending)
            return;

        var key = $"{kind} {detail}";
        pending.Changes[key] = pending.Changes.GetValueOrDefault(key) + 1;
    }

    /// <summary>
    /// Records that <paramref name="session"/> looked at <paramref name="target"/> through VV, batched
    /// into their next log line. Every admin's VV views are recorded, not just critical players'.
    /// </summary>
    private void ReportVvView(ICommonSession session, string target)
    {
        if (GetPendingVv(session) is not { } pending)
            return;

        pending.Views[target] = pending.Views.GetValueOrDefault(target) + 1;
    }

    private PendingVv? GetPendingVv(ICommonSession session)
    {
        if (!_adminManager.IsAdmin(session))
            return null;

        if (!_pendingVv.TryGetValue(session.UserId, out var pending))
        {
            pending = new PendingVv(session.Name, _timing.CurTime + VvBatchDelay);
            _pendingVv.Add(session.UserId, pending);
        }

        return pending;
    }

    private void UpdatePendingVv()
    {
        if (_pendingVv.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (user, pending) in _pendingVv)
        {
            if (now >= pending.FlushAt)
                _toFlushVv.Add(user);
        }

        foreach (var user in _toFlushVv)
        {
            FlushVv(_pendingVv[user]);
            _pendingVv.Remove(user);
        }

        _toFlushVv.Clear();
    }

    /// <summary>
    /// Flushes every admin's pending VV activity right away, e.g. so nothing is lost at round end.
    /// </summary>
    private void FlushAllPendingVv()
    {
        foreach (var pending in _pendingVv.Values)
            FlushVv(pending);

        _pendingVv.Clear();
    }

    /// <summary>
    /// Writes one plain-English admin-log line summarizing everything <paramref name="pending"/> collected.
    /// </summary>
    private void FlushVv(PendingVv pending)
    {
        var parts = new List<string>();

        if (pending.Changes.Count > 0)
        {
            var total = pending.Changes.Values.Sum();
            parts.Add($"made {total} VV change{(total == 1 ? "" : "s")}: {string.Join(", ", SummarizeVv(pending.Changes))}");
        }

        if (pending.Views.Count > 0)
        {
            var total = pending.Views.Values.Sum();
            parts.Add($"viewed {total} thing{(total == 1 ? "" : "s")}: {string.Join(", ", SummarizeVv(pending.Views))}");
        }

        if (parts.Count == 0)
            return;

        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium, $"{pending.Who} {string.Join("; ", parts)}");
    }

    private static List<string> SummarizeVv(Dictionary<string, int> counts)
    {
        var items = counts
            .OrderByDescending(kv => kv.Value)
            .Take(MaxListedVv)
            .Select(kv => kv.Value > 1 ? $"{kv.Value}x {kv.Key}" : kv.Key)
            .ToList();

        var hidden = counts.Count - items.Count;
        if (hidden > 0)
            items.Add($"{hidden} more");

        return items;
    }

    private void Throttle(ICommonSession session, string channel, string who, string kind)
    {
        var key = (session.UserId, channel);
        if (!_throttled.TryGetValue(key, out var alert))
        {
            alert = new ThrottledAlert();
            _throttled.Add(key, alert);
        }

        alert.Who = who;
        alert.Counts[kind] = alert.Counts.GetValueOrDefault(kind) + 1;

        if (_timing.CurTime >= alert.NextAllowed)
            Flush(channel, alert);
    }

    private void UpdateThrottled()
    {
        if (_throttled.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var ((_, channel), alert) in _throttled)
        {
            if (alert.Counts.Count > 0 && now >= alert.NextAllowed)
                Flush(channel, alert);
        }
    }

    private void Flush(string channel, ThrottledAlert alert)
    {
        var changes = alert.Counts.Select(kv =>
            Loc.GetString($"critical-alert-{channel}-{kv.Key}", ("count", kv.Value)));

        _chat.SendAdminAlert(Loc.GetString($"critical-alert-{channel}",
            ("who", alert.Who),
            ("changes", string.Join(", ", changes))));

        alert.Counts.Clear();
        alert.NextAllowed = _timing.CurTime + ThrottleInterval;
    }

    #endregion

    #region Antag data

    private void CheckSensitivePath(ICommonSession session, string path)
    {
        // Entity paths look like /entity/<net id>/<component>/...
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2
            || parts[0] != "entity"
            || !NetEntity.TryParse(parts[1], out var netEntity)
            || !TryGetEntity(netEntity, out var uid))
        {
            return;
        }

        CheckSensitiveView(session, uid.Value, parts.Length > 2 ? parts[2] : null);
    }

    /// <summary>
    /// If an admin just looked at antag data of a player through VV, treat it as an antag reveal of that player.
    /// </summary>
    private void CheckSensitiveView(ICommonSession admin, EntityUid uid, string? component)
    {
        if (!TryGetSensitiveMind(uid, component, out var mind)
            || mind.UserId is not { } target
            || target == admin.UserId
            || _antagReveal.IsRevealed(target))
        {
            return;
        }

        _antagReveal.Reveal(admin, target, Loc.GetString("antag-reveal-source-vv"));
    }

    private bool TryGetSensitiveMind(EntityUid uid, string? component, [NotNullWhen(true)] out MindComponent? mind)
    {
        // A mind or one of its role entities: everything on it is role data.
        if (TryComp(uid, out mind))
            return true;

        if (HasComp<MindRoleComponent>(uid)
            && _container.TryGetContainingContainer((uid, null, null), out var container)
            && TryComp(container.Owner, out mind))
        {
            return true;
        }

        // A body: only its antag components are sensitive.
        if (!_mind.TryGetMind(uid, out _, out mind))
            return false;

        return component != null
            ? _sensitiveNames.Contains(component)
            : AllComps(uid).Any(comp => _sensitiveTypes.Contains(comp.GetType()));
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AntagSpecifierPrototype>())
            RebuildSensitiveComponents();
    }

    private void RebuildSensitiveComponents()
    {
        _sensitiveNames.Clear();
        _sensitiveTypes.Clear();

        foreach (var registration in _compFactory.GetAllRegistrations())
        {
            if (registration.Type == typeof(MindComponent) || registration.Type.IsAssignableTo(typeof(BaseMindRoleComponent)))
                AddSensitive(registration);
        }

        var names = _proto.EnumeratePrototypes<AntagSpecifierPrototype>()
            .SelectMany(antag => antag.Components.Keys.Concat(antag.MindComponents.Keys))
            .Concat(ExtraSensitiveComponents);

        foreach (var name in names)
        {
            if (!GenericAntagComponents.Contains(name) && _compFactory.TryGetRegistration(name, out var registration))
                AddSensitive(registration);
        }
    }

    private void AddSensitive(ComponentRegistration registration)
    {
        _sensitiveNames.Add(registration.Name);
        _sensitiveTypes.Add(registration.Type);
    }

    #endregion

    private sealed class ThrottledAlert
    {
        public string Who = string.Empty;
        public TimeSpan NextAllowed;
        public readonly Dictionary<string, int> Counts = new();
    }

    private sealed class PendingVv(string who, TimeSpan flushAt)
    {
        public readonly string Who = who;
        public readonly TimeSpan FlushAt = flushAt;
        public readonly Dictionary<string, int> Changes = new();
        public readonly Dictionary<string, int> Views = new();
    }
}
