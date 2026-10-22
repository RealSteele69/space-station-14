using Content.Shared.Alert;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Silicons.Synths.Battery;

/// <summary>
/// A synth body that runs off the <see cref="SynthPowerCellComponent"/> organ inside it.
/// The cell drains over time, recharges from eating, shocks and cyborg recharging stations,
/// and the synth slows down while it's flat.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SynthBatteryComponent : Component
{
    /// <summary>
    /// Charge drained from the cell per second.
    /// </summary>
    [DataField]
    public float DrawRate = 0.4f;

    /// <summary>
    /// Charge gained per point of hunger satiated by food.
    /// </summary>
    [DataField]
    public float NutrimentChargeMultiplier = 2.4f;

    [DataField]
    public LocId? BatteryLowText;

    [DataField]
    public LocId? BatteryDeadText;

    [DataField]
    public LocId? BatteryEmpText = "synth-battery-emp";

    [DataField]
    public SoundSpecifier? BatteryLowSound;

    [DataField]
    public SoundSpecifier? BatteryDeadSound;

    /// <summary>
    /// Charge percentages that trigger a low-battery warning when the cell drains past them.
    /// </summary>
    [DataField]
    public List<float> WarningPercentages = new() { 20f, 10f };

    [DataField]
    public float UnpoweredWalkSpeedModifier = 0.5f;

    [DataField]
    public float UnpoweredSprintSpeedModifier = 0.5f;

    [DataField]
    public DamageSpecifier? EmpDamage;

    [DataField]
    public ProtoId<AlertPrototype> BatteryAlert = "SynthBattery";

    [DataField]
    public ProtoId<AlertPrototype> NoBatteryAlert = "SynthBatteryNone";

    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public bool Unpowered;

    [ViewVariables]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Whether the cell was last refreshed with a recharging station's charge rate applied.
    /// </summary>
    [ViewVariables]
    public bool InCharger;
}
