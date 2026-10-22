using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._ClawCommand.Zombies;

/// <summary>
/// A zombie infection that has gone critical. The entity turns on the spot when <see cref="TurnTime"/> is reached,
/// whether they are alive, in crit or not. Symptoms ramp up over the duration.
/// Removed again if the underlying infection is cured (Ambuzol removes PendingZombie).
/// </summary>
[RegisterComponent, Access(typeof(CriticalZombieInfectionSystem))]
[AutoGenerateComponentPause]
public sealed partial class CriticalZombieInfectionComponent : Component
{
    /// <summary>
    /// Shortest possible time from going critical to turning. Rolled once, on init.
    /// </summary>
    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Longest possible time from going critical to turning. Rolled once, on init.
    /// </summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromMinutes(30);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan StartTime;

    /// <summary>
    /// When the entity zombifies.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TurnTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextSymptom;

    /// <summary>
    /// Set while no player controls the entity; the countdown is pushed back by the paused time on return.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? PausedAt;

    /// <summary>
    /// Time between symptoms right after going critical.
    /// </summary>
    [DataField]
    public TimeSpan MaxSymptomInterval = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Time between symptoms right before turning.
    /// </summary>
    [DataField]
    public TimeSpan MinSymptomInterval = TimeSpan.FromSeconds(20);

    [DataField]
    public float MinCoughChance = 0.15f;

    [DataField]
    public float MaxCoughChance = 0.6f;

    /// <summary>
    /// Shaking chance scales from this to <see cref="MaxShakeChance"/>, so early on there is none.
    /// </summary>
    [DataField]
    public float MinShakeChance;

    [DataField]
    public float MaxShakeChance = 0.8f;

    [DataField]
    public float MinShakeAmplitude = 3f;

    [DataField]
    public float MaxShakeAmplitude = 15f;

    [DataField]
    public TimeSpan MinShakeDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan MaxShakeDuration = TimeSpan.FromSeconds(8);

    /// <summary>
    /// How long before turning the final warning is shown.
    /// </summary>
    [DataField]
    public TimeSpan FinalWarning = TimeSpan.FromSeconds(30);

    [DataField]
    public bool FinalWarningShown;

    [DataField]
    public ProtoId<EmotePrototype> CoughEmote = "Cough";

    /// <summary>
    /// Shown for the first third of the duration.
    /// </summary>
    [DataField]
    public List<LocId> EarlyMessages = new()
    {
        "critical-zombie-infection-early-1",
        "critical-zombie-infection-early-2",
        "critical-zombie-infection-early-3",
        "critical-zombie-infection-early-4",
        "critical-zombie-infection-early-5",
        "critical-zombie-infection-early-6",
        "critical-zombie-infection-early-7",
        "critical-zombie-infection-early-8",
        "critical-zombie-infection-twitch",
    };

    /// <summary>
    /// Shown for the middle third of the duration.
    /// </summary>
    [DataField]
    public List<LocId> MiddleMessages = new()
    {
        "critical-zombie-infection-middle-1",
        "critical-zombie-infection-middle-2",
        "critical-zombie-infection-middle-3",
        "critical-zombie-infection-middle-4",
        "critical-zombie-infection-middle-5",
        "critical-zombie-infection-middle-6",
        "critical-zombie-infection-middle-7",
        "critical-zombie-infection-middle-8",
        "critical-zombie-infection-twitch",
    };

    /// <summary>
    /// Shown for the last third of the duration.
    /// </summary>
    [DataField]
    public List<LocId> LateMessages = new()
    {
        "critical-zombie-infection-late-1",
        "critical-zombie-infection-late-2",
        "critical-zombie-infection-late-3",
        "critical-zombie-infection-late-4",
        "critical-zombie-infection-late-5",
        "critical-zombie-infection-late-6",
        "critical-zombie-infection-late-7",
        "critical-zombie-infection-late-8",
        "critical-zombie-infection-twitch",
    };

    /// <summary>
    /// Examine text from outside details range.
    /// </summary>
    [DataField]
    public LocId ExamineFarMessage = "critical-zombie-infection-examine-far";

    /// <summary>
    /// Examine text from up close.
    /// </summary>
    [DataField]
    public LocId ExamineCloseMessage = "critical-zombie-infection-examine-close";

    [DataField]
    public LocId FinalMessage = "critical-zombie-infection-final";

    [DataField]
    public LocId CuredMessage = "critical-zombie-infection-cured";
}
