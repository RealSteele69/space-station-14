namespace Content.Shared._ClawCommand.Supermatter.Events;

/// <summary>
///     Raised broadcast when a supermatter finishes delaminating.
/// </summary>
public sealed class SupermatterDelaminatedEvent(EntityUid supermatter) : EntityEventArgs
{
    public readonly EntityUid Supermatter = supermatter;
}
