using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Whimperlet;

/// <summary>
/// Periodically spills a little of a crying creature's tears at its feet while it is alive.
/// </summary>
public sealed partial class WhimperletSystem : EntitySystem
{
    [Dependency] private SharedPuddleSystem _puddles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WhimperletComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<WhimperletComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextDrop = _timing.CurTime + ent.Comp.DropInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<WhimperletComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime < comp.NextDrop)
                continue;

            comp.NextDrop = curTime + comp.DropInterval;

            if (!_mobState.IsAlive(uid))
                continue;

            var amount = FixedPoint2.Max(FixedPoint2.Zero, comp.VolumePerDrop);
            if (amount <= FixedPoint2.Zero)
                continue;

            var solution = new Solution();
            solution.AddReagent(comp.Reagent, amount);

            // Spill where the creature (or whoever is carrying it) actually is.
            _puddles.TrySpillAt(_transform.GetMoverCoordinates(uid), solution, out _, sound: false);
        }
    }
}
