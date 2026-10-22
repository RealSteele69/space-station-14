namespace Content.Server._ClawCommand.Blob.Components;

/// <summary>
/// Optional per-station override of the blob stage thresholds (counted in blob tiles).
/// Claw Command: the blob needs more tiles for every stage than in the original.
/// </summary>
[RegisterComponent]
public sealed partial class StationBlobConfigComponent : Component
{
    public const int DefaultStageBegin = 30;
    public const int DefaultShuttleLock = 75;
    public const int DefaultStageCritical = 450;
    public const int DefaultStageEnd = 1000;

    /// <summary>
    /// The blob is announced and the station goes to red.
    /// </summary>
    [DataField]
    public int StageBegin { get; set; } = DefaultStageBegin;

    /// <summary>
    /// From this size on the emergency shuttle gets recalled until it has docked.
    /// If the blob is cut back below it, the crew may call the shuttle again.
    /// </summary>
    [DataField]
    public int ShuttleLock { get; set; } = DefaultShuttleLock;

    /// <summary>
    /// Delta alert, nuke codes are sent.
    /// </summary>
    [DataField]
    public int StageCritical { get; set; } = DefaultStageCritical;

    /// <summary>
    /// The blob wins and the round ends.
    /// </summary>
    [DataField]
    public int StageTheEnd { get; set; } = DefaultStageEnd;
}
