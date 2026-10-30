using Content.Shared._ClawCommand.TapeRecorder.Systems;

namespace Content.Client._ClawCommand.TapeRecorder;

/// <summary>
/// Required for client side prediction stuff
/// </summary>
public sealed partial class TapeRecorderSystem : SharedTapeRecorderSystem
{
    private TimeSpan _lastTickTime = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        // We need to know the exact time period that has passed since the last update to ensure the tape position is
        // synced with the server. Since the client can skip frames when lagging, we cannot use frameTime.
        var realTime = (float) (Timing.CurTime - _lastTickTime).TotalSeconds;
        _lastTickTime = Timing.CurTime;

        base.Update(realTime);
    }
}
