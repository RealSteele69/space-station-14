using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.Events;

/// <summary>
/// Sent by a shuttle console to change the inertial dampener mode of the shuttle it's attached to.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetInertiaDampeningRequest : BoundUserInterfaceMessage
{
    public InertiaDampeningMode Mode;
}

/// <summary>
/// How much a shuttle's drift is countered when its pilot isn't actively thrusting.
/// </summary>
[Serializable, NetSerializable]
public enum InertiaDampeningMode : byte
{
    /// <summary>
    /// No assist. The shuttle keeps its momentum until thrust or a collision changes it.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Default assisted flight. Drift is bled off at the shuttle's normal rate.
    /// </summary>
    Dampened = 1,

    /// <summary>
    /// Hard brake. Drift is killed rapidly, similar to anchoring.
    /// </summary>
    Anchored = 2,
}
