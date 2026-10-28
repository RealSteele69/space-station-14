// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Mood;
using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireCandelabrumSystem : EntitySystem
{
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedMoodSystem _mood = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireCandelabrumComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<VampireCandelabrumComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<VampireCandelabrumComponent> candelabrum, ref MapInitEvent args)
    {
        _light.SetEnabled(candelabrum, candelabrum.Comp.Lit);
    }

    private void OnActivate(Entity<VampireCandelabrumComponent> candelabrum, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        candelabrum.Comp.Lit = !candelabrum.Comp.Lit;
        _light.SetEnabled(candelabrum, candelabrum.Comp.Lit);

        if (!candelabrum.Comp.Lit)
        {
            var nearby = _lookup.GetEntitiesInRange(candelabrum.Owner, candelabrum.Comp.Range);
            foreach (var entity in nearby)
                _mood.RemoveMoodlet(entity, candelabrum.Comp.Moodlet);
        }

        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<VampireCandelabrumComponent>();
        while (query.MoveNext(out var uid, out var candelabrum))
        {
            if (!candelabrum.Lit || now < candelabrum.NextTick)
                continue;

            candelabrum.NextTick = now + TickInterval;

            foreach (var entity in _lookup.GetEntitiesInRange(uid, candelabrum.Range))
            {
                if (!HasComp<HumanoidProfileComponent>(entity) || HasComp<VampireComponent>(entity) || HasComp<VassalComponent>(entity))
                    continue;

                _mood.AddMoodlet(entity, candelabrum.Moodlet);
            }
        }
    }
}
