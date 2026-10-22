using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Blob.Components;
/// <remarks>
/// To add a new special blob tile you will need to change code in BlobNodeSystem and BlobTypedStorage.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlobNodeComponent : Component
{
    [DataField]
    public float PulseFrequency = 5f;

    [DataField]
    public float PulseRadius = 4f;

    public float NextPulse = 0;

    /// <summary>
    /// Claw Command: chance per pulse that the node grows or chews on one adjacent thing for free.
    /// The original did this on every pulse, which let a blob spread with no points at all.
    /// </summary>
    [DataField]
    public float AutoExpandChance = 0.35f;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? BlobResource = null;
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? BlobFactory = null;
    /*
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? BlobStorage = null;
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? BlobTurret = null;
    */
}

public sealed class BlobTileGetPulseEvent : HandledEntityEventArgs
{
    /// <summary>
    /// Whether this tile may grow or attack somewhere for free on this pulse.
    /// </summary>
    public bool TryGrow;
}

[Serializable, NetSerializable]
public sealed partial class BlobMobGetPulseEvent : EntityEventArgs
{
    public NetEntity BlobEntity { get; set; }
}

/// <summary>
/// Event raised on all special tiles of Blob Node on pulse.
/// </summary>
public sealed class BlobSpecialGetPulseEvent : EntityEventArgs;

/// <summary>
/// Event
/// </summary>
public sealed class BlobNodePulseEvent : EntityEventArgs;