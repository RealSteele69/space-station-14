using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Silicons.Synths;

/// <summary>
/// Shock damage charges the synth's power cell, but also fries some of the organic parts.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SynthShockComponent : Component
{
    [DataField]
    public ProtoId<DamageTypePrototype> ShockDamageType = "Shock";

    /// <summary>
    /// Charge gained per point of shock damage taken.
    /// </summary>
    [DataField]
    public float BatteryChargeMultiplier = 2f;

    [DataField]
    public ProtoId<DamageTypePrototype> CellularDamageType = "Cellular";

    /// <summary>
    /// Cellular damage taken per point of shock damage.
    /// </summary>
    [DataField]
    public float CellularDamageMultiplier = 0.2f;
}
