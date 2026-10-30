using Content.Server.GameTicking;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Robust.Shared.Random;

namespace Content.Server.StationEvents;

/// <summary>
/// Handles ramping scheduling: starts other game rules more and more frequently as the round goes on.
/// </summary>
/// <seealso cref="RampingStationEventSchedulerComponent"/>
public sealed partial class RampingStationEventSchedulerSystem : GameRuleSystem<RampingStationEventSchedulerComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EventManagerSystem _event = default!;
    [Dependency] private ServerGameTicker _gameTicker = default!;

    /// <summary>
    /// Returns the ChaosModifier which increases as round time increases, then plateaus.
    /// </summary>
    public float GetChaosModifier(Entity<RampingStationEventSchedulerComponent> ent)
    {
        var roundTime = (float)_gameTicker.RoundDuration().TotalSeconds;

        // Claw Command - past the end time the ramp holds at the value it climbed to, rather
        // than dropping back by StartingChaos. Vanilla returned MaxChaos here, which made the
        // curve discontinuous and quietly calmed a round back down the moment it peaked.
        if (roundTime > ent.Comp.EndTime)
            return ent.Comp.MaxChaos + ent.Comp.StartingChaos;

        return ent.Comp.MaxChaos / ent.Comp.EndTime * roundTime + ent.Comp.StartingChaos;
    }

    protected override void Started(Entity<RampingStationEventSchedulerComponent, GameRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        base.Started(ent, ref args);

        var scheduler = ent.Comp1;

        // Worlds shittiest probability distribution
        // Got a complaint? Send them to
        scheduler.MaxChaos = _random.NextFloat(scheduler.AverageChaos - scheduler.AverageChaos / 4, scheduler.AverageChaos + scheduler.AverageChaos / 4);
        // This is in minutes, so *60 for seconds (for the chaos calc)
        scheduler.EndTime = _random.NextFloat(scheduler.AverageEndTime - scheduler.AverageEndTime / 4, scheduler.AverageEndTime + scheduler.AverageEndTime / 4) * 60f;
        // Claw Command - allow presets to decouple the opening event rate from the closing one.
        scheduler.StartingChaos = scheduler.InitialChaos ?? scheduler.MaxChaos / 10;

        PickNextEventTime(ent);
    }

    // TODO: GO THROUGH EVERY SINGLE GAME RULE AND JUST CLEAN THIS STUFF UP!!!
    protected override void ActiveTick(EntityUid entityUid, RampingStationEventSchedulerComponent component, GameRuleComponent gameRuleComponent, float frameTime)
    {
        if (!_event.EventsEnabled)
            return;

        if (component.TimeUntilNextEvent > 0f)
        {
            component.TimeUntilNextEvent -= frameTime;
            return;
        }

        PickNextEventTime((entityUid, component));
        _event.RunRandomEvent(component.ScheduledGameRules);
    }

    /// <summary>
    /// Sets the timing of the next event addition.
    /// </summary>
    private void PickNextEventTime(Entity<RampingStationEventSchedulerComponent> ent)
    {
        var mod = GetChaosModifier(ent);

        // 4-12 minutes baseline by default. Will get faster over time as the chaos mod increases.
        // Claw Command - the baseline band is configurable so presets can set their own pacing.
        ent.Comp.TimeUntilNextEvent = _random.NextFloat(ent.Comp.MinEventTime / mod, ent.Comp.MaxEventTime / mod);
    }
}
