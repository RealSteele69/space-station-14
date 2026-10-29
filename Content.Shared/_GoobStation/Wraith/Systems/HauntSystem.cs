// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
// Partially ported from Impstation (per source).
//
// Fork note: source flashed witnesses via the old StatusEffectsSystem.TryAddStatusEffect<FlashedComponent>
// generic, whose "Flashed" key/component this fork doesn't wire up the same way. Substituted with
// SharedFlashSystem.Flash, the real entry point this fork's flashbangs/flashes use. Also renamed
// HumanoidAppearanceComponent -> HumanoidProfileComponent (fork-wide rename).

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared._GoobStation.Wraith.WraithPoints;
using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Content.Shared.Flash;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffect;
using Robust.Shared.Timing;

namespace Content.Shared._GoobStation.Wraith.Systems;

public sealed partial class HauntSystem : EntitySystem
{
    [Dependency] private SharedInteractionSystem _interact = default!;
    [Dependency] private StatusEffectsSystem _statusEffectsOld = default!;
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private WraithPointsSystem _wraithPointsSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private EntityQuery<HauntedComponent> _hauntQuery;
    private EntityQuery<WraithAbsorbableComponent> _wraithAbsorbableQuery;

    private readonly HashSet<Entity<HumanoidProfileComponent>> _humanoid = new();
    private readonly HashSet<Entity<StatusEffectsComponent>> _statusEffects = new();

    public override void Initialize()
    {
        base.Initialize();

        _hauntQuery = GetEntityQuery<HauntedComponent>();
        _wraithAbsorbableQuery = GetEntityQuery<WraithAbsorbableComponent>();

        SubscribeLocalEvent<HauntComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HauntComponent, ComponentShutdown>(OnComponentShutdown);

        SubscribeLocalEvent<HauntComponent, HauntEvent>(OnHaunt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HauntComponent>();
        while (query.MoveNext(out var uid, out var haunt))
        {
            if (_timing.CurTime >= haunt.NextHauntWpRegenUpdate && haunt.WpBoostActive)
            {
                // reset
                _wraithPointsSystem.SetWpRate(haunt.OriginalWpRegen, uid);
                haunt.WpBoostActive = false;
                Dirty(uid, haunt);
            }

            if (!haunt.Active)
                continue;

            if (_timing.CurTime >= haunt.NextHauntUpdate)
            {
                RemComp<CorporealComponent>(uid);
                haunt.Active = false;
                _actions.StartUseDelay(haunt.ActionEnt);

                Dirty(uid, haunt);
            }

            // constantly check for witnesses
            if (_timing.CurTime >= haunt.WitnessNextUpdate)
            {
                _humanoid.Clear();
                _lookup.GetEntitiesInRange(Transform(uid).Coordinates, 10f, _humanoid); // 10f should cover your view-range
                foreach (var entity in _humanoid)
                {
                    // skip if we are already haunted, or if we cant be haunted
                    if (_hauntQuery.HasComp(entity) || !_wraithAbsorbableQuery.HasComp(entity) || _mobState.IsDead(entity))
                        continue;

                    if (!_interact.InRangeUnobstructed(uid, entity.Owner, 10f))
                        continue;

                    EnsureComp<HauntedComponent>(entity);
                    _wraithPointsSystem.AdjustWpGenerationRate(haunt.HauntWpRegenPerWitness, uid);
                }

                haunt.WitnessNextUpdate = _timing.CurTime + haunt.WitnessUpdate;
                Dirty(uid, haunt);
            }
        }
    }

    private void OnHaunt(Entity<HauntComponent> ent, ref HauntEvent args)
    {
        if (ent.Comp.Active)
        {
            RemComp<CorporealComponent>(ent.Owner);
            _wraithPointsSystem.SetWpRate(ent.Comp.OriginalWpRegen, ent.Owner);
            ent.Comp.Active = false;
            ent.Comp.WpBoostActive = false;
            args.Handled = true;
            Dirty(ent);

            return;
        }

        _popupSystem.PopupClient(Loc.GetString("wraith-haunt-show"), ent.Owner, ent.Owner, PopupType.MediumCaution);

        // flash people nearby
        _statusEffects.Clear();
        _lookup.GetEntitiesInRange(Transform(ent.Owner).Coordinates, 3f, _statusEffects);
        foreach (var entity in _statusEffects)
            _flash.Flash(entity, ent.Owner, null, ent.Comp.HauntFlashDuration, ent.Comp.FlashSlowTo, displayPopup: false);

        // we don't have corporeal so add it
        _statusEffectsOld.TryAddStatusEffect<CorporealComponent>(ent.Owner, ent.Comp.CorporealEffect, ent.Comp.HauntCorporealDuration, true);

        // set original rate for resetting it after boost
        ent.Comp.OriginalWpRegen = _wraithPointsSystem.GetCurrentWpRate(ent.Owner);

        // activate the haunt timer and start tracking people
        ent.Comp.Active = true;
        ent.Comp.NextHauntUpdate = _timing.CurTime + ent.Comp.HauntDuration;
        ent.Comp.WitnessNextUpdate = _timing.CurTime + ent.Comp.WitnessUpdate;

        // boost wp regen per witness
        ent.Comp.NextHauntWpRegenUpdate = _timing.CurTime + ent.Comp.HauntWpRegenDuration;
        ent.Comp.WpBoostActive = true;
        Dirty(ent);
    }

    private void OnMapInit(Entity<HauntComponent> ent, ref MapInitEvent args) =>
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEnt, ent.Comp.ActionId);

    private void OnComponentShutdown(Entity<HauntComponent> ent, ref ComponentShutdown args) =>
        _actions.RemoveAction(ent.Comp.ActionEnt);
}
