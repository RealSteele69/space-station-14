using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Silicons.Synths.Body;

/// <summary>
/// Synth self-repair: nanites in the blood slowly fix damage, seal wounds and regrow the blood itself,
/// paid for out of the body's hunger. Repairs slow down as blood runs low and stop when starving.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SynthBloodstreamComponent : Component
{
    /// <summary>
    /// Hunger at or below which no repairs happen.
    /// </summary>
    [DataField]
    public float MinHunger = 50f;

    /// <summary>
    /// Blood level (0-1) below which repairs stop entirely.
    /// </summary>
    [DataField]
    public float MinBloodLevel = 0.3f;

    /// <summary>
    /// Blood level (0-1) at which repairs run at full speed.
    /// </summary>
    [DataField]
    public float FullEfficiencyBloodLevel = 0.9f;

    /// <summary>
    /// Repair speed multiplier just above <see cref="MinBloodLevel"/>.
    /// </summary>
    [DataField]
    public float MinBloodEfficiency = 0.1f;

    /// <summary>
    /// Hunger spent per point of damage repaired.
    /// </summary>
    [DataField]
    public float HungerCostPerRepair = 1.5f;

    /// <summary>
    /// Hunger spent per point of bleeding sealed.
    /// </summary>
    [DataField]
    public float HungerCostPerBleed = 1f;

    /// <summary>
    /// Repairs per second at full efficiency. Values are negative.
    /// </summary>
    [DataField]
    public SynthBloodstreamDamageSpecifier Damage = new();

    /// <summary>
    /// Bleeding sealed per second at full efficiency.
    /// </summary>
    [DataField]
    public float BleedReductionAmount = 0.1f;

    [DataField]
    public SynthBloodstreamRegeneration BloodRegeneration = new();

    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextUpdate;
}

[DataDefinition]
public sealed partial class SynthBloodstreamDamageSpecifier
{
    [DataField]
    public Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> Groups = new();

    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Types = new();
}

[DataDefinition]
public sealed partial class SynthBloodstreamRegeneration
{
    /// <summary>
    /// Blood level (0-1) regeneration tops out at.
    /// </summary>
    [DataField]
    public float TargetBloodLevel = 1f;

    /// <summary>
    /// Units of blood regrown per update.
    /// </summary>
    [DataField]
    public FixedPoint2 BloodRefreshAmount = FixedPoint2.New(0.06f);

    /// <summary>
    /// Hunger spent per unit of blood regrown.
    /// </summary>
    [DataField]
    public float HungerCostPerUnit = 0.25f;
}
