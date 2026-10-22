// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Lavaland.Aggression;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Components;
using Robust.Shared.Player;

namespace Content.Server._ClawCommand.Lavaland.Megafauna.Systems;

public sealed partial class MegafaunaAttacksSystem
{
    private readonly HashSet<Entity<ActorComponent>> _aggroCandidates = new();

    private void InitializeAggro()
    {
        SubscribeLocalEvent<MegafaunaAggroRadiusComponent, MapInitEvent>(OnAggroMapInit);
    }

    private void OnAggroMapInit(Entity<MegafaunaAggroRadiusComponent> ent, ref MapInitEvent args)
        => ent.Comp.NextCheck = _timing.CurTime + ent.Comp.Interval;

    private void UpdateAggro(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<MegafaunaAggroRadiusComponent, AggressiveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aggro, out var aggressive, out var xform))
        {
            if (aggro.NextCheck > curTime)
                continue;

            aggro.NextCheck = curTime + aggro.Interval;

            if (!_mobState.IsAlive(uid))
                continue;

            _aggroCandidates.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, aggro.Radius, _aggroCandidates);
            foreach (var candidate in _aggroCandidates)
            {
                var player = candidate.Owner;
                if (player == uid
                    || aggressive.Aggressors.Contains(player)
                    || !_mobState.IsAlive(player)
                    || !_examine.InRangeUnOccluded(uid, player, aggro.Radius))
                    continue;

                _aggressors.AddAggressor((uid, aggressive), player);
            }
        }
    }
}
