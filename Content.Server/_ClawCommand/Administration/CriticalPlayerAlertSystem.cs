using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Network;
using Robust.Shared.Placement;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Administration;

/// <summary>
/// Tells admin chat when a staff member who has been a critical player this round (command, security, ERT,
/// admiral or an antagonist) aghosts, spawns entities or edits things through view variables.
/// Once someone held a critical role they stay flagged until the round ends, whatever body or ghost they are
/// in now, so respawning or taking a body back through admin commands does not hide anything.
/// Everything is batched or throttled per player so admin chat only gets short notices; details go to admin logs.
/// </summary>
public sealed partial class CriticalPlayerAlertSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private AntagRevealSystem _antagReveal = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _role = default!;

    private static readonly ProtoId<DepartmentPrototype>[] CriticalDepartments = ["Command", "Security"];

    /// <summary>
    /// Jobs in a critical department that still do not count, e.g. CentComm officials listed under Command.
    /// </summary>
    private static readonly ProtoId<DepartmentPrototype> ExcludedDepartment = "CentralCommand";

    /// <summary>
    /// Critical jobs outside the critical departments (or inside the excluded one).
    /// </summary>
    private static readonly HashSet<ProtoId<JobPrototype>> ExtraCriticalJobs =
    [
        "ERTLeader",
        "ERTChaplain",
        "ERTJanitor",
        "ERTMedical",
        "ERTSecurity",
        "ERTEngineer",
        "CBURN",
        "DeathSquad",
        "AdmiralClaw",
    ];

    /// <summary>
    /// How long spawns are collected after the first one before they are sent as one alert.
    /// </summary>
    private static readonly TimeSpan SpawnBatchDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How many different spawned entity names are listed before the rest is summarized as "+N more".
    /// </summary>
    private const int MaxListedSpawns = 6;

    /// <summary>
    /// Everyone who held a critical role at any point this round.
    /// </summary>
    private readonly Dictionary<NetUserId, CriticalRecord> _critical = new();

    private readonly Dictionary<NetUserId, PendingSpawns> _pendingSpawns = new();
    private readonly List<NetUserId> _toFlush = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<PlacementEntityEvent>(OnPlacement);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        InitializeViewVariables();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        ShutdownViewVariables();
    }

    /// <summary>
    /// Call when <paramref name="player"/> is moved into an admin ghost (not when they return to their body).
    /// </summary>
    /// <param name="actor">Whoever ran the command, if it was not the player themselves.</param>
    public void AlertAGhost(ICommonSession player, ICommonSession? actor)
    {
        if (!TryDescribe(player, out var who))
            return;

        _chat.SendAdminAlert(actor != null && actor != player
            ? Loc.GetString("critical-alert-aghost-other", ("who", who), ("actor", actor.Name))
            : Loc.GetString("critical-alert-aghost", ("who", who)));
    }

    private void OnRoleAdded(RoleAddedEvent ev)
    {
        if (ev.Mind.UserId is { } user)
            Record(user, ev.MindId, ev.Mind);
    }

    private void OnPlacement(PlacementEntityEvent ev)
    {
        if (ev.PlacementEventAction != PlacementEventAction.Create
            || !_player.TryGetSessionById(ev.PlacerNetUserId, out var session)
            || !_adminManager.IsAdmin(session)
            || TerminatingOrDeleted(ev.EditedEntity))
        {
            return;
        }

        if (!_pendingSpawns.TryGetValue(session.UserId, out var pending))
        {
            if (!TryDescribe(session, out var who))
                return;

            pending = new PendingSpawns(who, _timing.CurTime + SpawnBatchDelay);
            _pendingSpawns.Add(session.UserId, pending);
        }

        var meta = MetaData(ev.EditedEntity);
        var name = meta.EntityPrototype?.Name ?? meta.EntityName;
        pending.Counts[name] = pending.Counts.GetValueOrDefault(name) + 1;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateThrottled();

        if (_pendingSpawns.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (user, pending) in _pendingSpawns)
        {
            if (now >= pending.FlushAt)
                _toFlush.Add(user);
        }

        foreach (var user in _toFlush)
        {
            var pending = _pendingSpawns[user];
            _pendingSpawns.Remove(user);

            var items = pending.Counts
                .OrderByDescending(kv => kv.Value)
                .Take(MaxListedSpawns)
                .Select(kv => kv.Value > 1 ? $"{kv.Value}x {kv.Key}" : kv.Key)
                .ToList();

            var hidden = pending.Counts.Count - items.Count;
            if (hidden > 0)
                items.Add(Loc.GetString("critical-alert-spawned-more", ("count", hidden)));

            _chat.SendAdminAlert(Loc.GetString("critical-alert-spawned",
                ("who", pending.Who),
                ("items", string.Join(", ", items))));
        }

        _toFlush.Clear();
    }

    /// <summary>
    /// Adds any critical job or antag roles the mind currently has to the player's record for this round.
    /// </summary>
    private void Record(NetUserId user, EntityUid mindId, MindComponent mind)
    {
        string? job = null;
        if (_job.MindTryGetJob(mindId, out var jobProto) && IsCriticalJob(jobProto.ID))
            job = jobProto.LocalizedName;

        var antags = _role.MindGetAllRoleInfo((mindId, mind))
            .Where(role => role.Antagonist)
            .Select(role => Loc.GetString(role.Name))
            .ToList();

        if (job == null && antags.Count == 0)
            return;

        if (!_critical.TryGetValue(user, out var record))
        {
            record = new CriticalRecord();
            _critical.Add(user, record);
        }

        if (job != null)
            record.Jobs.Add(job);

        record.Antags.UnionWith(antags);
        record.Character = mind.CharacterName ?? record.Character;
    }

    private bool IsCriticalJob(ProtoId<JobPrototype> job)
    {
        if (ExtraCriticalJobs.Contains(job))
            return true;

        return !_job.JobIsInDepartment(job, ExcludedDepartment)
               && CriticalDepartments.Any(dept => _job.JobIsInDepartment(job, dept));
    }

    private bool IsCritical(ICommonSession player)
    {
        if (_mind.TryGetMind(player, out var mindId, out var mind))
            Record(player.UserId, mindId, mind);

        return _critical.ContainsKey(player.UserId);
    }

    /// <summary>
    /// Builds "player (character, roles)" if the player has been critical at any point this round.
    /// Antag roles are only named once the player's antag status has been revealed to admins.
    /// </summary>
    private bool TryDescribe(ICommonSession player, out string who)
    {
        who = string.Empty;

        if (!IsCritical(player))
            return false;

        var record = _critical[player.UserId];
        var roles = new List<string>(record.Jobs);
        if (record.Antags.Count > 0)
        {
            if (_antagReveal.IsRevealed(player.UserId))
                roles.AddRange(record.Antags);
            else
                roles.Add(Loc.GetString("critical-alert-hidden-antag"));
        }

        who = Loc.GetString("critical-alert-player",
            ("player", player.Name),
            ("character", record.Character ?? Loc.GetString("generic-unknown")),
            ("roles", string.Join(", ", roles)));
        return true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _critical.Clear();
        _pendingSpawns.Clear();
        _throttled.Clear();
        _vvSessions.Clear();
    }

    private sealed class CriticalRecord
    {
        public readonly SortedSet<string> Jobs = new();
        public readonly SortedSet<string> Antags = new();
        public string? Character;
    }

    private sealed class PendingSpawns(string who, TimeSpan flushAt)
    {
        public readonly string Who = who;
        public readonly TimeSpan FlushAt = flushAt;
        public readonly Dictionary<string, int> Counts = new();
    }
}
