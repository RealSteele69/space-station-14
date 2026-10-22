namespace Content.Shared._ClawCommand.Supermatter.Events;

/// <summary>
///     Raised on an entity right before the supermatter dusts it.
/// </summary>
[ByRefEvent]
public readonly record struct SupermatterConsumedEvent(EntityUid Supermatter);
