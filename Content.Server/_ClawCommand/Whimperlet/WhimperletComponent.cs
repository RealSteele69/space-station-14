using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._ClawCommand.Whimperlet;

/// <summary>
/// Makes a creature constantly cry, dripping tears at its feet while it is alive.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(WhimperletSystem))]
public sealed partial class WhimperletComponent : Component
{
    /// <summary>
    /// Time between drops.
    /// </summary>
    [DataField]
    public TimeSpan DropInterval = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    /// Amount of reagent spilled per drop.
    /// </summary>
    [DataField]
    public FixedPoint2 VolumePerDrop = FixedPoint2.New(0.5f);

    /// <summary>
    /// The reagent the tears are made of.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Saline";

    /// <summary>
    /// When the next drop falls.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDrop;
}
