namespace Content.Server._ClawCommand.StationEvents.Components;

/// <summary>
/// While present on a grid, gravity generators cannot give it gravity.
/// Added and removed by <see cref="GravityFailureRuleComponent"/> rules.
/// </summary>
[RegisterComponent]
public sealed partial class GravitySuppressedComponent : Component;
