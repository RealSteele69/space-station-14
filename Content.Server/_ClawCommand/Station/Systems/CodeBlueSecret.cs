using Content.Server.ClawCommand.Cabinet.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.AlertLevel;
using Content.Shared.GameTicking;
using Color = Robust.Shared.Maths.Color;

namespace Content.Server._ClawCommand.Station.Systems;

/// <summary>
/// Five minutes into a secret round, raises each station from green to blue once.
/// After that the crew is free to go back to green, or anywhere else, without it being forced again.
/// </summary>
public sealed partial class CodeBlueSecretSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;

    private TimeSpan _acoDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Stations already handled this round, whether they were raised to blue or were above green at the time.
    /// </summary>
    private readonly HashSet<EntityUid> _handled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _handled.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        if (_ticker.RoundDuration() < _acoDelay)
            return;

        if (!_ticker.IsGameRuleAdded<SecretRuleComponent>())
            return;

        var query = EntityQueryEnumerator<CaptainStateComponent>();
        while (query.MoveNext(out var station, out _))
        {
            if (_handled.Contains(station) || !_alertLevelSystem.TryGetLevel(station, out var level))
                continue;

            _handled.Add(station);

            if (level == "Green")
            {
                _alertLevelSystem.SetLevel(station, "Blue", playSound: true, announce: false, force: true);
                _chatSystem.DispatchStationAnnouncement(station,
                    "Enemy communications intercepted. Suspected security threat to the station or its crew. Crewmembers are advised to follow commands issued by any relevant authority.",
                    colorOverride: Color.DodgerBlue,
                    sender: "Claw Command",
                    playDefaultSound: false);
            }
        }
    }
}
