// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note (significant rewrite): source used Content.Shared._Shitmed.Medical.Surgery.Wounds to
// literally sever a random limb every few hits and amputate the head on the finishing hit. This
// fork has no _Shitmed and no detachable-limb body system at all (see
// Content.Shared.Body.OrganComponent/BodySystem - it's a flat organ-relation model, not
// slotted BodyPart entities), so there is nothing to amputate. The "sever a limb" beat became a
// burst of bonus damage plus the delimb sound/popup, and the "decapitate" finishing beat became a
// bigger burst of bonus damage plus the decap sound/popup - the kill itself still flows through
// normal damage into the vanilla MobState system (consistent with how the rest of this fork
// handles death), rather than forcibly deleting the victim via GibbingSystem.

using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._GoobStation.Slasher.Systems;

/// <summary>
/// System for the massacre action. Can be re-used for other similar weapons.
/// </summary>
public sealed partial class SlasherMassacreSystem : EntitySystem
{
    public static EntProtoId SlasherEffect = "SlasherStatusEffectKillSpeed";

    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MovementModStatusSystem _movementModStatus = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlasherMassacreMacheteComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<SlasherMassacreUserComponent, SlasherMassacreEvent>(OnMassacreAction);
        SubscribeLocalEvent<SlasherMassacreMacheteComponent, MeleeHitEvent>(OnMeleeHitWeapon);
        SubscribeLocalEvent<SlasherMassacreVictimComponent, MobStateChangedEvent>(OnVictimMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SlasherMassacreUserComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active || comp.LastAttackTime == null)
                continue;

            var timeSinceLastAttack = _timing.CurTime - comp.LastAttackTime.Value;
            if (timeSinceLastAttack.TotalSeconds >= comp.ChainTimeoutSeconds)
                EndChain(uid, comp, true);
        }
    }

    private void OnGetItemActions(EntityUid uid, SlasherMassacreMacheteComponent comp, GetItemActionsEvent args)
    {
        EnsureComp<SlasherMassacreUserComponent>(args.User);

        if (_net.IsServer)
            args.AddAction(ref comp.MassacreActionEntity, comp.MassacreActionId);

        Dirty(uid, comp);
    }

    private void OnMassacreAction(Entity<SlasherMassacreUserComponent> ent, ref SlasherMassacreEvent args)
    {
        if (!ent.Comp.Active)
        {
            ent.Comp.Active = true;
            ent.Comp.HitCount = 0;
            ent.Comp.CurrentVictim = null;
            ent.Comp.LastAttackTime = _timing.CurTime;

            _popup.PopupPredicted(Loc.GetString("slasher-massacre-start"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            _audio.PlayPredicted(ent.Comp.MassacreIntro, ent.Owner, ent.Owner);

            _actions.StartUseDelay((EntityUid?) args.Action);

            if (TryGetMachete(ent.Owner, out var weaponUid, out var melee))
            {
                melee.CanWideSwing = false;
                Dirty(weaponUid, melee);
            }
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("slasher-massacre-already-activated"), ent.Owner, ent.Owner, PopupType.Medium);
        }

        Dirty(ent);
    }

    private void EndChain(EntityUid uid, SlasherMassacreUserComponent comp, bool showPopup = false)
    {
        if (comp.Active && showPopup)
            _popup.PopupPredicted(Loc.GetString("slasher-massacre-end"), uid, uid, PopupType.MediumCaution);

        if (comp.CurrentVictim != null && TryComp<SlasherMassacreVictimComponent>(comp.CurrentVictim.Value, out _))
            RemCompDeferred<SlasherMassacreVictimComponent>(comp.CurrentVictim.Value);

        if (TryGetMachete(uid, out var weaponUid, out var weapon))
        {
            weapon.CanWideSwing = true;
            Dirty(weaponUid, weapon);
        }

        comp.Active = false;
        comp.HitCount = 0;
        comp.CurrentVictim = null;
        comp.LastAttackTime = null;
        Dirty(uid, comp);
    }

    private void OnVictimMobStateChanged(EntityUid uid, SlasherMassacreVictimComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (comp.Attacker != null
            && comp.WeaponComp != null
            && TryComp<SlasherMassacreUserComponent>(comp.Attacker.Value, out var userComp)
            && userComp.Active
            && userComp.CurrentVictim == uid
            && userComp.HitCount >= 1)
        {
            _audio.PlayPredicted(comp.MassacreFinale, comp.Attacker.Value, comp.Attacker.Value);
            _popup.PopupPredicted(Loc.GetString("slasher-massacre-decap"), comp.Attacker.Value, comp.Attacker.Value, PopupType.MediumCaution);

            ApplyKillBonuses(comp.Attacker.Value, userComp.HitCount, comp.WeaponComp);

            EndChain(comp.Attacker.Value, userComp);
        }

        RemCompDeferred<SlasherMassacreVictimComponent>(uid);
    }

    private void OnMeleeHitWeapon(Entity<SlasherMassacreMacheteComponent> weaponEnt, ref MeleeHitEvent args)
    {
        if (!TryComp<SlasherMassacreUserComponent>(args.User, out var userComp) || !userComp.Active)
            return;

        if (args.HitEntities.Count == 0)
        {
            EndChain(args.User, userComp, true);
            return;
        }

        EntityUid? victim = null;
        foreach (var hit in args.HitEntities)
        {
            if (!HasComp<HumanoidProfileComponent>(hit))
                continue;

            victim = hit;
            break;
        }

        if (victim == null)
        {
            EndChain(args.User, userComp, true);
            return;
        }

        if (_mobState.IsDead(victim.Value))
        {
            _popup.PopupPredicted(Loc.GetString("slasher-massacre-end"), args.User, args.User, PopupType.MediumCaution);
            EndChain(args.User, userComp, true);
            return;
        }

        if (userComp.CurrentVictim != null && userComp.CurrentVictim != victim.Value)
        {
            _popup.PopupPredicted(Loc.GetString("slasher-massacre-target-change"), args.User, args.User, PopupType.MediumCaution);

            if (TryComp<SlasherMassacreVictimComponent>(userComp.CurrentVictim.Value, out _))
                RemCompDeferred<SlasherMassacreVictimComponent>(userComp.CurrentVictim.Value);

            userComp.HitCount = 0;
        }

        userComp.CurrentVictim = victim.Value;
        userComp.HitCount++;
        userComp.LastAttackTime = _timing.CurTime;

        var victimComp = EnsureComp<SlasherMassacreVictimComponent>(victim.Value);
        victimComp.Attacker = args.User;
        victimComp.WeaponComp = weaponEnt.Comp;
        Dirty(victim.Value, victimComp);

        // Calculate damage bonus/penalty.
        var totalBonus = -weaponEnt.Comp.BaseDamagePenalty + weaponEnt.Comp.PerHitBonus * (userComp.HitCount - 1);

        var playedDelimb = false;

        // Big damage burst every LimbSeverHits ("sever a limb" beat - no body parts on this fork,
        // so this is just a harder hit with its own sound/popup).
        if (userComp.HitCount >= weaponEnt.Comp.LimbSeverHits && userComp.HitCount % weaponEnt.Comp.LimbSeverHits == 0)
        {
            totalBonus += weaponEnt.Comp.PerHitBonus * 3;
            playedDelimb = true;
            if (_net.IsServer)
                _popup.PopupEntity(Loc.GetString("slasher-massacre-limb"), victim.Value, PopupType.Medium);
        }

        // Finishing beat ("decapitate" - a devastating burst of damage).
        if (userComp.HitCount == weaponEnt.Comp.DecapitateHit)
        {
            totalBonus += weaponEnt.Comp.PerHitBonus * 8;
            playedDelimb = true;
        }

        if (totalBonus != 0)
        {
            var spec = new DamageSpecifier();
            spec.DamageDict.Add("Slash", totalBonus);
            args.BonusDamage += spec;
        }

        if (_net.IsServer)
        {
            if (playedDelimb)
                _audio.PlayPvs(weaponEnt.Comp.MassacreDelimb, args.User);
            else
                _audio.PlayPvs(weaponEnt.Comp.MassacreSlash, args.User);
        }

        Dirty(args.User, userComp);
    }

    /// <summary>
    /// Applies speed and health bonuses when a victim dies during massacre mode.
    /// Bonus scales with hit count.
    /// </summary>
    private void ApplyKillBonuses(EntityUid user, int hitCount, SlasherMassacreMacheteComponent comp)
    {
        if (!_net.IsServer || hitCount <= 0)
            return;

        var speedBonus = comp.SpeedBonusPerHit * hitCount;
        var healAmount = comp.HealAmountPerHit * hitCount;

        if (speedBonus > 0)
            _movementModStatus.TryUpdateMovementSpeedModDuration(user, SlasherEffect, TimeSpan.FromSeconds(comp.SpeedBoostDuration), 1 + speedBonus);

        if (healAmount > 0 && TryComp<BloodstreamComponent>(user, out var bloodstream)
            && _solutions.ResolveSolution(user, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution))
            _solutions.TryAddReagent(bloodstream.BloodSolution.Value,
                new ReagentId(comp.HealReagent, null),
                FixedPoint2.New(healAmount),
                out _);
    }

    /// <summary>
    /// Tries to get the machete held by the user.
    /// </summary>
    private bool TryGetMachete(EntityUid user, out EntityUid weaponUid, [NotNullWhen(true)] out MeleeWeaponComponent? melee)
    {
        weaponUid = default;
        melee = null;

        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (HasComp<SlasherMassacreMacheteComponent>(held) && TryComp<MeleeWeaponComponent>(held, out var weapon))
            {
                weaponUid = held;
                melee = weapon;
                return true;
            }
        }

        return false;
    }
}
