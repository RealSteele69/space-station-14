using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._ClawCommand.AACTablet.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class AACTabletComponent : Component
{
    /// <summary>
    /// Minimum time between each phrase, to prevent spam
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Time that the next phrase can be sent.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPhrase;
}

/// <summary>
/// Cancellable event for attempting to use an AAC tablet, raised on the user.
/// </summary>
/// <param name="Tablet">The tablet which will be opened if successful.</param>
/// <param name="FailReason">The reason why it failed.</param>
/// <param name="Cancelled">Whether the attempt to open it failed.</param>
[ByRefEvent]
public record struct AACTabletOpenAttemptEvent(EntityUid Tablet, LocId? FailReason = null, bool Cancelled = false);
