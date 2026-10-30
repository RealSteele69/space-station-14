using Content.Server._ClawCommand.StationEvents.Components;
using Content.Server.Gravity;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Gravity;
using Content.Shared.Station.Components;

namespace Content.Server._ClawCommand.StationEvents.Events;

/// <summary>
/// A feedback surge in the mass-distribution system: gravity drops out across one station
/// for the duration of the event and comes back on its own when it ends.
/// Grids with inherent gravity (planets) are left alone.
/// </summary>
public sealed partial class GravityFailureRule : StationEventSystem<GravityFailureRuleComponent>
{
    [Dependency] private GravitySystem _gravity = default!;

    protected override void Started(EntityUid uid, GravityFailureRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!Station.TryGetRandomStation(out var station))
            return;

        var data = station.Value.Comp;

        foreach (var grid in data.Grids)
        {
            if (!TryComp<GravityComponent>(grid, out var gravity) || gravity.Inherent || !gravity.Enabled)
                continue;

            EnsureComp<GravitySuppressedComponent>(grid);
            component.AffectedGrids.Add(grid);
            _gravity.RefreshGravity(grid, gravity);
        }
    }

    protected override void Ended(Entity<GravityFailureRuleComponent> rule, ref GameRuleEndedEvent args)
    {
        var uid = rule.Owner;
        var component = rule.Comp;
        base.Ended(rule, ref args);

        foreach (var grid in component.AffectedGrids)
        {
            if (TerminatingOrDeleted(grid))
                continue;

            RemComp<GravitySuppressedComponent>(grid);
            _gravity.RefreshGravity(grid);
        }

        component.AffectedGrids.Clear();
    }
}
