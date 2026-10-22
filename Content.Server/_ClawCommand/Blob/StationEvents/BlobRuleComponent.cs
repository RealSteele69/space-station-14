using Content.Server.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Blob.StationEvents;

[RegisterComponent, Access(typeof(BlobSpawnRule))]
public sealed partial class BlobSpawnRuleComponent : Component
{
    [DataField("carrierBlobProtos", required: true), ViewVariables(VVAccess.ReadWrite)]
    public List<EntProtoId> CarrierBlobProtos = new()
    {
        "SpawnPointGhostBlobRat"
    };

    [ViewVariables(VVAccess.ReadOnly), DataField("playersPerCarrierBlob")]
    public int PlayersPerCarrierBlob = 40;

    [ViewVariables(VVAccess.ReadOnly), DataField("maxCarrierBlob")]
    public int MaxCarrierBlob = 1;
}