namespace Content.Shared._ClawCommand.GangConversion;

/// <summary>
/// Raised on a potential recruit before the gang tattoo gun converts them, mirroring
/// <c>AttemptConvertRevolutionaryEvent</c>. Immunity components (mindshield, etc.) can cancel it.
/// </summary>
[ByRefEvent]
public record struct AttemptConvertGangEvent(bool Cancelled = false);
