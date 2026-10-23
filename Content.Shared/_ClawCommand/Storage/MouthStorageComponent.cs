using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Storage;

/// <summary>
///     Claw Command - Gives a mob a small storage pouch in its mouth (rodent cheek pouches).
///     The pouch is a separate storage entity held in a hidden container on the mob, opened with an action.
///     While anything is inside, the mob mumbles and cannot eat or drink, and it spits everything out
///     when it falls over, is shoved, or takes a solid hit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(MouthStorageSystem))]
public sealed partial class MouthStorageComponent : Component
{
    public const string MouthContainerId = "mouth";

    /// <summary>
    ///     Action that opens and closes the pouch.
    /// </summary>
    [DataField]
    public EntProtoId? OpenStorageAction;

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    /// <summary>
    ///     The storage entity spawned into the mouth.
    /// </summary>
    [DataField]
    public EntProtoId MouthProto = "CheekStorage";

    [ViewVariables]
    public Container Mouth = default!;

    [DataField, AutoNetworkedField]
    public EntityUid? MouthId;

    /// <summary>
    ///     Minimum damage from a single hit that makes the mob spit out what it is holding.
    /// </summary>
    [DataField]
    public FixedPoint2 SpitDamageThreshold = FixedPoint2.New(2);
}
