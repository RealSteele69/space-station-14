using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._ClawCommand.Administration;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Administration;

/// <summary>
/// Hides antagonist status from admins until it is explicitly revealed for a player. A reveal is scoped to
/// the admin who triggered it, not shared with every other admin, so one admin looking someone up doesn't
/// unmask them for the rest of the team; a console reveal has no admin to scope to and unmasks for everyone.
/// Every reveal (admin panel button or a command that prints antag info) is admin-logged and
/// announced in admin chat, so looking up who the antags are is never silent.
/// </summary>
public sealed partial class AntagRevealSystem : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private AdminSystem _admin = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Per-admin reveals: a target revealed to one admin stays masked for every other admin who hasn't
    /// separately revealed them. Keyed by target, not admin, since lookups are almost always "is this
    /// target revealed to this admin".
    /// </summary>
    private readonly Dictionary<NetUserId, HashSet<NetUserId>> _revealedTo = new();

    /// <summary>
    /// Targets revealed with no specific admin attached (server console). Console has no personal view to
    /// scope a reveal to, so a console reveal is treated as visible to every admin, same as round end.
    /// </summary>
    private readonly HashSet<NetUserId> _revealedGlobally = new();

    /// <summary>
    /// Reveals in quick succession (an admin clicking the button repeatedly) are coalesced into a single
    /// log entry and admin alert instead of one each, so the log/chat doesn't get spammed.
    /// </summary>
    private static readonly TimeSpan BatchWindow = TimeSpan.FromSeconds(3);
    private readonly List<PendingReveal> _pending = new();
    private TimeSpan _pendingFlushAt;

    private readonly record struct PendingReveal(string AdminName, string PlayerName, string Character, NetUserId Target, string Source, string Result);

    /// <summary>
    /// Admin log types that state antag status outright. In the current round they are hidden from the
    /// log viewer unless every player on the log is revealed, and they never raise admin chat alerts.
    /// Hiding depends only on the log type and reveal state, never on whether someone is an antag,
    /// so a missing entry gives nothing away.
    /// </summary>
    public static readonly HashSet<LogType> HiddenLogTypes = [LogType.AntagSelection, LogType.MindRole];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestAntagRevealEvent>(OnRequestReveal);
        // Clear before AdminSystem rebuilds and resends the player list for the new round.
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup, before: [typeof(AdminSystem)]);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    /// <summary>
    /// Whether <paramref name="admin"/> may currently see this player's antag status. Everything is visible
    /// once the round has ended, or if the target was revealed from the console (which has no personal view
    /// to scope a reveal to, so it unmasks for every admin).
    /// </summary>
    public bool IsRevealed(NetUserId admin, NetUserId target)
    {
        if (_ticker.RunLevel == GameRunLevel.PostRound || _revealedGlobally.Contains(target))
            return true;

        return _revealedTo.TryGetValue(target, out var admins) && admins.Contains(admin);
    }

    /// <inheritdoc cref="IsRevealed(NetUserId,NetUserId)"/>
    /// <param name="admin">The admin, or null for the server console.</param>
    public bool IsRevealed(ICommonSession? admin, NetUserId target)
    {
        if (_ticker.RunLevel == GameRunLevel.PostRound || _revealedGlobally.Contains(target))
            return true;

        return admin != null && IsRevealed(admin.UserId, target);
    }

    /// <summary>
    /// Whether this player's antag status is visible to every admin at once (round end or a console reveal),
    /// as opposed to only the specific admins who revealed them. Use this for anything broadcast to admins
    /// as a group (e.g. a single admin-chat alert) that can't be masked differently per recipient.
    /// </summary>
    public bool IsRevealedGlobally(NetUserId target)
    {
        return _ticker.RunLevel == GameRunLevel.PostRound || _revealedGlobally.Contains(target);
    }

    /// <summary>
    /// Returns <paramref name="info"/> as <paramref name="admin"/> should see it: unmasked if revealed to
    /// them, antag status and role type stripped out otherwise.
    /// </summary>
    public PlayerInfo Mask(PlayerInfo info, NetUserId admin)
    {
        if (IsRevealed(admin, info.SessionId))
            return info with { AntagRevealed = true };

        return info with
        {
            Antag = false,
            RoleProto = null,
            Subtype = null,
            SortWeight = 0,
            AntagRevealed = false,
        };
    }

    /// <summary>
    /// Records that <paramref name="admin"/> learned <paramref name="target"/>'s antag status through
    /// <paramref name="source"/>: admin-logs it, announces it in admin chat and, the first time it happens,
    /// unmasks the player in the admin player list for <paramref name="admin"/> (or for every admin, if this
    /// came from the console) for the rest of the round. Call this from anything that shows antag info to an admin.
    /// </summary>
    /// <param name="admin">The admin, or null for the server console.</param>
    public void Reveal(ICommonSession? admin, NetUserId target, string source)
    {
        // The round-end summary shows everything anyway.
        if (_ticker.RunLevel == GameRunLevel.PostRound)
            return;

        var adminName = admin?.Name ?? Loc.GetString("antag-reveal-console");
        var playerName = _player.TryGetPlayerData(target, out var data) ? data.UserName : target.ToString();
        var character = string.Empty;
        var antagRoles = new List<string>();

        if (_mind.TryGetMind(target, out var mindId, out var mind))
        {
            character = mind.CharacterName ?? string.Empty;
            foreach (var role in _role.MindGetAllRoleInfo(mindId.Value))
            {
                if (role.Antagonist)
                    antagRoles.Add(Loc.GetString(role.Name));
            }
        }

        var result = antagRoles.Count > 0
            ? string.Join(", ", antagRoles.Distinct())
            : Loc.GetString("antag-reveal-none");

        // The log entry and chat alert are queued, not sent right away, so repeated reveals in a short
        // window (someone clicking the panel button down a player list) land as one entry, not a flood.
        _pending.Add(new PendingReveal(adminName, playerName, character, target, source, result));
        _pendingFlushAt = _timing.CurTime + BatchWindow;

        bool newlyRevealed;
        if (admin != null)
        {
            if (!_revealedTo.TryGetValue(target, out var admins))
                _revealedTo[target] = admins = new HashSet<NetUserId>();

            newlyRevealed = admins.Add(admin.UserId);
        }
        else
        {
            newlyRevealed = _revealedGlobally.Add(target);
        }

        // Only push a player-list update the first time this admin (or everyone, for a console reveal)
        // actually gains visibility; repeat lookups still log/alert above but have nothing new to unmask.
        if (newlyRevealed)
            _admin.RefreshPlayerInfo(target, admin);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0 || _timing.CurTime < _pendingFlushAt)
            return;

        FlushPending();
    }

    private void FlushPending()
    {
        if (_pending.Count == 0)
            return;

        if (_pending.Count == 1)
        {
            var r = _pending[0];
            _adminLog.Add(LogType.AntagReveal, LogImpact.High,
                $"{r.AdminName} revealed antag status of {r.PlayerName} ({r.Target}) via {r.Source}: {r.Result}");

            _chat.SendAdminAlert(Loc.GetString("antag-reveal-admin-alert",
                ("admin", r.AdminName),
                ("player", r.PlayerName),
                ("character", r.Character == string.Empty ? string.Empty : Loc.GetString("antag-reveal-character", ("name", r.Character))),
                ("source", r.Source)));
        }
        else
        {
            var adminNames = string.Join(", ", _pending.Select(r => r.AdminName).Distinct());
            var details = string.Join("; ", _pending.Select(r => $"{r.PlayerName} ({r.Target}) via {r.Source}: {r.Result}"));

            _adminLog.Add(LogType.AntagReveal, LogImpact.High,
                $"{adminNames} revealed antag status of {_pending.Count} players: {details}");

            _chat.SendAdminAlert(Loc.GetString("antag-reveal-admin-alert-batch",
                ("admin", adminNames),
                ("count", _pending.Count)));
        }

        _pending.Clear();
    }

    /// <summary>
    /// For views that only expose antag status indirectly (e.g. a subverted silicon's laws):
    /// reveals the controlling player only if they are a hidden antagonist.
    /// </summary>
    public void RevealIfAntag(ICommonSession? admin, EntityUid body, string source)
    {
        if (!_mind.TryGetMind(body, out var mindId, out var mind) || mind.UserId is not { } userId)
            return;

        if (admin?.UserId == userId || IsRevealed(admin, userId) || !_role.MindIsAntagonist(mindId))
            return;

        Reveal(admin, userId, source);
    }

    /// <summary>
    /// Restricts an admin log query so it can't show antag status of players not revealed to
    /// <paramref name="admin"/> in the current round. Call once when the filter is created; the reveal
    /// state is snapshotted so paging stays consistent.
    /// </summary>
    public void ApplyLogMask(LogFilter filter, NetUserId admin)
    {
        if (_ticker.RunLevel == GameRunLevel.PostRound)
            return;

        // A null round means "all rounds", which includes this one.
        if (filter.Round != null && filter.Round != _ticker.RoundId)
            return;

        filter.HiddenTypes = HiddenLogTypes;

        var visible = _revealedGlobally.Select(u => u.UserId).ToList();
        foreach (var (target, admins) in _revealedTo)
        {
            if (admins.Contains(admin))
                visible.Add(target.UserId);
        }

        filter.VisiblePlayers = visible.ToArray();
    }

    private void OnRequestReveal(RequestAntagRevealEvent ev, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
            return;

        // The panel button is one-shot per player per admin per round.
        if (IsRevealed(args.SenderSession, ev.Target))
            return;

        Reveal(args.SenderSession, ev.Target, Loc.GetString("antag-reveal-source-panel"));
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        FlushPending();
        _revealedTo.Clear();
        _revealedGlobally.Clear();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        // Round over: unmask everyone in the admin player list.
        if (ev.New == GameRunLevel.PostRound)
            _admin.RefreshAllPlayerInfo();
    }
}
