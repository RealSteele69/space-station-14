using Content.Shared._ClawCommand.Blob.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Blob.Components;

[RegisterComponent]
public sealed partial class BlobFactoryComponent : Component
{
    [DataField("spawnLimit"), ViewVariables(VVAccess.ReadWrite)]
    public float SpawnLimit = 2;

    /// <summary>
    /// Pod limit while the blob uses Sinewy Tendons.
    /// </summary>
    [DataField]
    public float SinewySpawnLimit = 3;

    [DataField("blobSporeId"), ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId<BlobMobComponent> Pod = "MobBlobPod";

    [DataField("blobbernautId"), ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId<BlobbernautComponent> BlobbernautId = "MobBlobBlobbernaut";

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Blobbernaut = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> BlobPods = new ();

    [DataField]
    public int Accumulator = 0;

    [DataField]
    public int AccumulateToSpawn = 4;
}

public sealed class ProduceBlobbernautEvent : EntityEventArgs
{
}
