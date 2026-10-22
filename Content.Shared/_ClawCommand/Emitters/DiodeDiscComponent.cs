using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Emitters;

/// <summary>
///     Used on an emitter to permanently change the bolts it fires.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(DiodeDiscSystem))]
public sealed partial class DiodeDiscComponent : Component
{
    /// <summary>
    ///     The bolt the emitter fires once the disc is installed.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId NewBolt;

    /// <summary>
    ///     Examine text shown on the upgraded emitter.
    /// </summary>
    [DataField(required: true)]
    public LocId Upgrade;

    /// <summary>
    ///     How long the doafter is
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Sound played when the disc is installed.
    /// </summary>
    [DataField]
    public SoundSpecifier? UpgradeSound = new SoundPathSpecifier("/Audio/Items/rped.ogg");
}

/// <summary>
///     Added to an emitter with a diode disc installed. Prevents stacking discs and shows which one it has.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(DiodeDiscSystem))]
public sealed partial class DiodeDiscInstalledComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public LocId Upgrade;
}

/// <summary>
///     Projectiles with this change the supermatter's damage and power when they hit it.
/// </summary>
[RegisterComponent]
public sealed partial class AngeringProjectileComponent : Component
{
    /// <summary>
    ///     Added to the supermatter's damage. Negative values repair it.
    /// </summary>
    [DataField]
    public float? IntegDamage;

    /// <summary>
    ///     Added to the supermatter's power. Negative values drain it.
    /// </summary>
    [DataField]
    public float? EnergyDamage;
}

[Serializable, NetSerializable]
public sealed partial class DiodeDiscDoAfterEvent : SimpleDoAfterEvent;
