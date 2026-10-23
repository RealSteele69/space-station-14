using Content.Server._ClawCommand.StationEvents.Events;

namespace Content.Server._ClawCommand.StationEvents.Components;

/// <summary>
/// Station event that knocks out artificial gravity on every grid of one station for the length of the event.
/// The generators keep running; their output is suppressed until the rule ends.
/// </summary>
[RegisterComponent, Access(typeof(GravityFailureRule))]
public sealed partial class GravityFailureRuleComponent : Component
{
    /// <summary>
    /// Grids this rule suppressed, so it only restores the ones it touched.
    /// </summary>
    [DataField]
    public List<EntityUid> AffectedGrids = new();
}
