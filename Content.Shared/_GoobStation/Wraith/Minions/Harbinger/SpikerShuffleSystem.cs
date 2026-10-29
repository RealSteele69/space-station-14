// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using System.Linq;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._GoobStation.Wraith.Minions.Harbinger;

public sealed partial class SpikerShuffleSystem : EntitySystem
{
    [Dependency] private Content.Shared.StatusEffect.StatusEffectsSystem _statusOld = default!;
    [Dependency] private StatusEffectsSystem _statusNew = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpikerShuffleComponent, SpikerShuffleEvent>(OnSpikerShuffle);

        SubscribeLocalEvent<SpikerShuffleEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<SpikerShuffleEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnSpikerShuffle(Entity<SpikerShuffleComponent> ent, ref SpikerShuffleEvent args)
    {
        // first remove all status effects
        foreach (var statusEffect in ent.Comp.StatusEffectsToRemove)
            _statusOld.TryRemoveStatusEffect(ent.Owner, statusEffect);

        _statusNew.TryAddStatusEffectDuration(ent.Owner, ent.Comp.StatusEffect, ent.Comp.Duration);
        _statusNew.TryAddStatusEffectDuration(ent.Owner, ent.Comp.StatusAbilityDisable, ent.Comp.Duration); // disable using actions

        args.Handled = true;
    }

    private void OnApplied(Entity<SpikerShuffleEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _popup.PopupClient(Loc.GetString("wraith-spiker-shuffle"), args.Target, args.Target, PopupType.Medium);
        _appearance.SetData(args.Target, ShuffleVisuals.Shuffling, true);

        if (TryComp<FixturesComponent>(args.Target, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var fixture = fixtures.Fixtures.First();

            _physics.SetCollisionMask(args.Target, fixture.Key, fixture.Value, (int)CollisionGroup.SmallMobMask, fixtures);
            _physics.SetCollisionLayer(args.Target, fixture.Key, fixture.Value, (int)CollisionGroup.SmallMobLayer, fixtures);
        }
    }

    private void OnRemoved(Entity<SpikerShuffleEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _popup.PopupClient(Loc.GetString("wraith-spiker-shuffle-removed"), args.Target, args.Target, PopupType.Medium);
        _appearance.SetData(args.Target, ShuffleVisuals.Shuffling, false);

        if (TryComp<FixturesComponent>(args.Target, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var fixture = fixtures.Fixtures.First();

            _physics.SetCollisionMask(args.Target, fixture.Key, fixture.Value, (int)CollisionGroup.MobMask, fixtures);
            _physics.SetCollisionLayer(args.Target, fixture.Key, fixture.Value, (int)CollisionGroup.MobLayer, fixtures);
        }
    }
}
