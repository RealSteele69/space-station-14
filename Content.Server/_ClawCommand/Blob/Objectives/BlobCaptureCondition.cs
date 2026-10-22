using Content.Server._ClawCommand.Blob.Components;

namespace Content.Server._ClawCommand.Blob.Objectives;

[RegisterComponent]
public sealed partial class BlobCaptureConditionComponent : Component
{
    [DataField]
    public int Target { get; set; } = StationBlobConfigComponent.DefaultStageEnd;
}