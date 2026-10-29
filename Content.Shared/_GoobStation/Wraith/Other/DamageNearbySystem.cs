// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source passed targetPart: TargetBodyPart.All from Content.Shared._Shitmed.Targeting, which
// doesn't exist here (no slotted body-part targeting system at all on this fork). Dropped the targetPart
// argument entirely - plain DamageableSystem.TryChangeDamage already applies to the whole entity.

using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Timing;

namespace Content.Shared._GoobStation.Wraith.Other;

public sealed partial class DamageNearbySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobStates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageNearbyComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var eqe = EntityQueryEnumerator<DamageNearbyComponent>();
        while (eqe.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextTick)
                continue;

            TryDamage((uid, comp));
            comp.NextTick = _timing.CurTime + comp.Delay;
            Dirty(uid, comp);
        }
    }

    private void OnMapInit(Entity<DamageNearbyComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + ent.Comp.Delay;
        Dirty(ent);
    }

    private void TryDamage(Entity<DamageNearbyComponent> ent)
    {
        _mobStates.Clear();
        _lookup.GetEntitiesInRange(Transform(ent.Owner).Coordinates, ent.Comp.Range, _mobStates);
        foreach (var entity in _mobStates)
        {
            if (!_whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, entity))
                continue;

            _damageable.TryChangeDamage(entity.Owner, ent.Comp.Damage);
        }
    }
}
