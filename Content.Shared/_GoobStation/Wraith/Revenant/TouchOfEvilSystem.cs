// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source used Content.Shared._White.Grab.GrabThrownSystem for the "throw everyone you melee
// hit" effect; substituted with the vanilla ThrowingSystem.

using System.Linq;
using System.Numerics;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._GoobStation.Wraith.Revenant;

public sealed partial class TouchOfEvilSystem : EntitySystem
{
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TouchOfEvilComponent, TouchOfEvilEvent>(OnTouchOfEvil);
        SubscribeLocalEvent<TouchOfEvilComponent, MeleeHitEvent>(OnMeleeHit);

        SubscribeLocalEvent<ActiveTouchOfEvilComponent, StatusEffectRemovedEvent>(StatusRemoved);
        SubscribeLocalEvent<ActiveTouchOfEvilComponent, StatusEffectAppliedEvent>(StatusApplied);
    }

    private void OnTouchOfEvil(Entity<TouchOfEvilComponent> ent, ref TouchOfEvilEvent args)
    {
        _statusEffects.TryAddStatusEffectDuration(ent.Owner, ent.Comp.TouchOfEvilEffect, ent.Comp.BuffDuration);

        args.Handled = true;
    }

    private void OnMeleeHit(Entity<TouchOfEvilComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.HitEntities.Any()
            || !ent.Comp.Active)
            return;

        foreach (var entity in args.HitEntities)
        {
            _throwing.TryThrow(entity,
                GetThrowDirection(ent.Owner, entity),
                ent.Comp.ThrowSpeed,
                ent.Owner);
        }
    }

    private void StatusApplied(Entity<ActiveTouchOfEvilComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp<TouchOfEvilComponent>(args.Target, out var touch)
            || !TryComp<MeleeWeaponComponent>(args.Target, out var melee))
            return;

        _popups.PopupClient(Loc.GetString("revenant-touch-of-evil-start"), args.Target, args.Target, PopupType.LargeCaution);
        _admin.Add(LogType.Action, LogImpact.Low, $"{args.Target}'s Touch of Evil duration has started");

        touch.OriginalDamage = melee.Damage;
        touch.Active = true;
        ent.Comp.ThrowSpeed = touch.ThrowSpeed;
        Dirty(args.Target, touch);

        melee.Damage *= touch.DamageBuff;
        Dirty(args.Target, melee);
    }

    private void StatusRemoved(Entity<ActiveTouchOfEvilComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!TryComp<TouchOfEvilComponent>(args.Target, out var touch)
            || !TryComp<MeleeWeaponComponent>(args.Target, out var melee)
            || touch.OriginalDamage == null)
            return;

        _popups.PopupClient(Loc.GetString("revenant-touch-of-evil-end"), args.Target, args.Target, PopupType.Medium);
        _admin.Add(LogType.Action, LogImpact.Low, $"{args.Target}'s Touch of Evil duration has ended");

        melee.Damage = touch.OriginalDamage;
        Dirty(args.Target, melee);

        touch.Active = false;
        touch.OriginalDamage = null;

        Dirty(args.Target, touch);
    }

    private Vector2 GetThrowDirection(EntityUid user, EntityUid target)
    {
        var entPos = _transform.GetMapCoordinates(user).Position;
        var targetPos = _transform.GetMapCoordinates(target).Position;
        return targetPos - entPos;
    }
}
