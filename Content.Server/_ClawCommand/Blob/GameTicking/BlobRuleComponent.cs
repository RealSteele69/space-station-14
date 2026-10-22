using Content.Server.GameTicking.Rules;
using Content.Shared.Mind;
using Robust.Shared.Audio;

namespace Content.Server._ClawCommand.Blob.GameTicking;

[RegisterComponent, Access(typeof(BlobRuleSystem), typeof(BlobCoreSystem), typeof(BlobObserverSystem))]
public sealed partial class BlobRuleComponent : Component
{
    [DataField]
    public SoundSpecifier? DetectedAudio = new SoundPathSpecifier("/Audio/_ClawCommand/Blob/blob_detected.ogg");

    [DataField]
    public SoundSpecifier? CriticalAudio = new SoundPathSpecifier("/Audio/_ClawCommand/Blob/blobin_time.ogg");

    [ViewVariables]
    public List<(EntityUid mindId, MindComponent mind)> Blobs = new(); //BlobRoleComponent

    [ViewVariables]
    public BlobStage Stage = BlobStage.Default;

    [ViewVariables]
    public float Accumulator = 0f;

    /// <summary>
    /// The "evacuate now" announcement only goes out once instead of every check.
    /// </summary>
    [ViewVariables]
    public bool ShuttleArrivedAnnounced;
}


public enum BlobStage : byte
{
    Default,
    Begin,
    Critical,
    TheEnd,
}