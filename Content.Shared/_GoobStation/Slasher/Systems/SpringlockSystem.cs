// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note (significant rewrite): source subscribed directly to a reaction event
// (a Goobstation-only "ShitRelayEventFixMeReactionEntityEvent", a workaround for a bug in their
// fork's reaction system) on ReactiveComponent/ReactionEntityEvent. On this fork that exact
// component+event pair is already claimed by Content.Shared.EntityEffects.SharedEntityEffectsSystem
// (the central reagent-reaction dispatcher) - subscribing to it a second time throws
// "Duplicate Subscriptions" at startup. This fork's reaction framework is declarative: a
// ReactiveComponent on an entity lists EntityEffects to run when touched, and each EntityEffect
// type has a paired EntityEffectSystem that does the actual work (see e.g.
// Content.Shared.EntityEffects.Effects.Damage.EvenHealthChangeEntityEffectSystem for the pattern).
// Also, source's trigger target was "the wearer gets touched by liquid, then scan their
// inventory for springlock items" - replicating that would mean adding a reaction to the shared
// humanoid body prototypes (out of this port's additive-only scope). Instead the springlock
// items themselves (apron/mask) carry the ReactiveComponent + effect directly, so the trigger is
// "liquid touches the springlock garment" rather than "wearer gets wet anywhere" - a narrower but
// equivalent-in-practice trigger (see Resources/Prototypes/_GoobStation/Slasher/items.yml).

using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;

namespace Content.Shared._GoobStation.Slasher.Systems;

/// <summary>
/// EntityEffect that locks a springlock garment shut. See the file header for why this is an
/// EntityEffect rather than a direct reaction-event subscription.
/// </summary>
public sealed partial class SpringlockTriggerEntityEffect : EntityEffectBase<SpringlockTriggerEntityEffect>;

/// <summary>
/// Handles the SpringlockTriggerEntityEffect: locks the springlock clothing it's attached to.
/// </summary>
public sealed partial class SpringlockTriggerEntityEffectSystem : EntityEffectSystem<SpringlockClothingComponent, SpringlockTriggerEntityEffect>
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    protected override void Effect(Entity<SpringlockClothingComponent> entity, ref EntityEffectEvent<SpringlockTriggerEntityEffect> args)
    {
        if (entity.Comp.IsLocked)
            return;

        entity.Comp.IsLocked = true;
        Dirty(entity);

        _appearance.SetData(entity.Owner, SpringlockVisuals.Locked, true);
        _audio.PlayPredicted(entity.Comp.LockSound, entity.Owner, null);
    }
}
