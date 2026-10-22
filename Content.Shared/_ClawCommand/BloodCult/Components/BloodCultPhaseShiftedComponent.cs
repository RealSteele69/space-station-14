using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._ClawCommand.BloodCult.Components;

/// <summary>
/// A wraith slipped into the veil: it passes through walls, is hidden from the living and moves faster.
/// Ends on its own after <see cref="EndTime"/>, or early when the wraith attacks or throws something.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BloodCultPhaseShiftedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float MovementSpeedBuff = 1.5f;

    [DataField]
    public int CollisionMask = (int) CollisionGroup.GhostImpassable;

    [DataField]
    public int CollisionLayer;

    [DataField]
    public EntProtoId PhaseInEffect = "CultTeleportInEffect";

    [DataField]
    public EntProtoId PhaseOutEffect = "CultTeleportOutEffect";

    [DataField]
    public SoundSpecifier PhaseInSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/enter_blood.ogg");

    [DataField]
    public SoundSpecifier PhaseOutSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/exit_blood.ogg");

    [ViewVariables]
    public int StoredMask;

    [ViewVariables]
    public int StoredLayer;
}
