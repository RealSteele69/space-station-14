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
/// Hides antagonist status from admins until it is explicitly revealed for a player.
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

    private readonly HashSet<NetUserId> _revealed = new();

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
    /// Whether admins may currently see this player's antag status. Everything is visible once the round has ended.
    /// </summary>
    public bool IsRevealed(NetUserId user)
    {
        return _ticker.RunLevel == GameRunLevel.PostRound || _revealed.Contains(user);
    }

    /// <summary>
    /// Records that <paramref name="admin"/> learned <paramref name="target"/>'s antag status through
    /// <paramref name="source"/>: admin-logs it, announces it in admin chat and unmasks the player in the
    /// admin player list for the rest of the round. Call this from anything that shows antag info to an admin.
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

        if (_revealed.Add(target))
            _admin.RefreshPlayerInfo(target);
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

        if (admin?.UserId == userId || IsRevealed(userId) || !_role.MindIsAntagonist(mindId))
            return;

        Reveal(admin, userId, source);
    }

    /// <summary>
    /// Restricts an admin log query so it can't show antag status of unrevealed players in the current round.
    /// Call once when the filter is created; the reveal state is snapshotted so paging stays consistent.
    /// </summary>
    public void ApplyLogMask(LogFilter filter)
    {
        if (_ticker.RunLevel == GameRunLevel.PostRound)
            return;

        // A null round means "all rounds", which includes this one.
        if (filter.Round != null && filter.Round != _ticker.RoundId)
            return;

        filter.HiddenTypes = HiddenLogTypes;
        filter.VisiblePlayers = _revealed.Select(u => u.UserId).ToArray();
    }

    private void OnRequestReveal(RequestAntagRevealEvent ev, EntitySessionEventArgs args)
    {
        if (!_adminManager.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
            return;

        // The panel button is one-shot per player per round.
        if (IsRevealed(ev.Target))
            return;

        Reveal(args.SenderSession, ev.Target, Loc.GetString("antag-reveal-source-panel"));
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        FlushPending();
        _revealed.Clear();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        // Round over: unmask everyone in the admin player list.
        if (ev.New == GameRunLevel.PostRound)
            _admin.RefreshAllPlayerInfo();
    }
}
