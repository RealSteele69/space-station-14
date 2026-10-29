// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: dropped the unused `using Content.Goobstation.Shared.Changeling.Components;` and
// unused SharedBodySystem dependency from source - Changeling isn't ported on this fork and
// neither was actually referenced by any method body in source either.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared._GoobStation.Wraith.WraithPoints;
using Content.Shared.Administration.Logs;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._GoobStation.Wraith.Systems;

public sealed partial class AbsorbCorpseSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private WraithPointsSystem _wraithPoints = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbsorbCorpseComponent, AbsorbCorpseEvent>(OnAbsorb);
        SubscribeLocalEvent<PlaguebringerComponent, AbsorbCorpseAttemptEvent>(OnPlaguebringerAttempt);
    }

    private void OnAbsorb(Entity<AbsorbCorpseComponent> ent, ref AbsorbCorpseEvent args)
    {
        var user = args.Performer;
        var target = args.Target;

        if (_tag.HasTag(args.Target, ent.Comp.Tag) || !TryComp<WraithAbsorbableComponent>(args.Target, out var absorbable)) // save the monkeys
            return;

        if (!_mobState.IsDead(target))
        {
            _popup.PopupClient(Loc.GetString("wraith-absorb-living"), user, user);
            return;
        }

        // user already absorbed, stop there
        if (absorbable.Absorbed)
        {
            _popup.PopupClient(Loc.GetString("wraith-absorb-already"), ent.Owner, ent.Owner);
            return;
        }

        var ev = new AbsorbCorpseAttemptEvent(args.Target);
        RaiseLocalEvent(args.Performer, ref ev);
        if (ev.Cancelled)
            return;

        if (ev.Handled)
        {
            absorbable.Absorbed = true;
            Dirty(args.Target, absorbable);

            _admin.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(ent.Owner)} absorbed the corpse of {ToPrettyString(args.Target)} as a Plaguebringer Wraith");
            args.Handled = true;
            return;
        }

        if (_rotting.IsRotten(target))
        {
            _popup.PopupClient(Loc.GetString("wraith-absorb-too-decomposed"), user, user);
            return;
        }

        // do reagent checking logic, if true activate cooldown
        if (RemoveReagent(args.Target, ent))
        {
            args.Handled = true;
            return;
        }

        // Spawn visual/sound effects
        PredictedSpawnAtPosition(ent.Comp.SmokeProto, Transform(target).Coordinates);
        _audio.PlayPredicted(ent.Comp.AbsorbSound, ent.Owner, user);

        _wraithPoints.AdjustWpGenerationRate(ent.Comp.WpPassiveAdd, ent.Owner);

        _popup.PopupPredicted(Loc.GetString("wraith-absorb-smoke1"), target, target);
        ent.Comp.CorpsesAbsorbed++;
        Dirty(ent);

        // mark as absorbed
        absorbable.Absorbed = true;
        Dirty(args.Target, absorbable);

        _admin.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent.Owner)} absorbed the corpse of {ToPrettyString(args.Target)} as a Wraith");
        args.Handled = true;
    }

    #region Special

    private void OnPlaguebringerAttempt(Entity<PlaguebringerComponent> ent, ref AbsorbCorpseAttemptEvent args)
    {
        if (!TryComp<PerishableComponent>(args.Target, out var perish)
            || !TryComp<DamageableComponent>(args.Target, out var damageable))
            return;

        var toxinDamage = _damageable.GetDamagePerGroup((args.Target, damageable)).GetValueOrDefault("Toxin");

        if (toxinDamage >= 60 || perish.Stage > 2)
        {
            _wraithPoints.AdjustWraithPoints(150, ent.Owner);
            _wraithPoints.AdjustWpGenerationRate(0.2, ent.Owner);

            _popup.PopupClient(Loc.GetString("wraith-absorb-rotbonus"), ent.Owner, ent.Owner, PopupType.Medium);
        }
        else if (toxinDamage < 30 && perish.Stage <= 2)
        {
            _popup.PopupClient(Loc.GetString("wraith-absorb-fresh"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            args.Cancelled = true;
        }

        args.Handled = true;
    }

    #endregion

    #region Helper
    private bool RemoveReagent(EntityUid target, Entity<AbsorbCorpseComponent> ent)
    {
        if (!TryComp<BloodstreamComponent>(target, out var blood)
            || !_solution.ResolveSolution(target, blood.BloodSolutionName, ref blood.BloodSolution, out var chemSolution))
            return false;

        foreach (var (reagentId, qty) in chemSolution.Contents)
        {
            if (reagentId.Prototype != ent.Comp.Reagent || qty < ent.Comp.FormaldehydeThreshhold)
                continue;

            _solution.RemoveReagent(blood.BloodSolution.Value, reagentId, ent.Comp.ChemToRemove);

            _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage, ignoreResistances: true);
            _popup.PopupClient(Loc.GetString("wraith-absorb-tainted"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return true;
        }

        return false;
    }
    #endregion

    #region Public
    public void Reset(Entity<AbsorbCorpseComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.CorpsesAbsorbed = 0;
        Dirty(ent);
    }
    #endregion
}
