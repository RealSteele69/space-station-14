// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._GoobStation.Slasher.Systems;

public sealed partial class SlasherRegenerateSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherRegenerateComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlasherRegenerateComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SlasherRegenerateComponent, SlasherRegenerateEvent>(OnRegenerate);
    }

    private void OnMapInit(Entity<SlasherRegenerateComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEnt, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnShutdown(Entity<SlasherRegenerateComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEnt);
    }

    /// <summary>
    /// Handles the regeneration of the entity/slasher (self) &amp; uncuffing.
    /// </summary>
    private void OnRegenerate(EntityUid uid, SlasherRegenerateComponent comp, SlasherRegenerateEvent args)
    {
        if (args.Handled)
            return;

        if (!comp.HasSoulAvailable)
        {
            _popup.PopupClient(Loc.GetString("slasher-regenerate-no-soul"), uid, uid);
            return;
        }

        var wasDead = _mobState.IsDead(uid);

        if (_net.IsServer)
        {
            RaiseLocalEvent(uid, new RejuvenateEvent());
            TryInjectReagent(uid, comp);

            var effectEnt = Spawn(comp.RegenerateEffect, _transform.GetMapCoordinates(uid));
            _transform.SetParent(effectEnt, uid);

            if (wasDead)
                ReviveFromDeath((uid, comp));
        }

        _audio.PlayPredicted(comp.RegenerateSound, uid, uid);

        comp.HasSoulAvailable = false;
        Dirty(uid, comp);

        args.Handled = true;
    }

    /// <summary>
    /// Shows the regenerate overlay to everyone in range and lets other systems know the slasher
    /// came back from death (e.g. to relocate them away from where they died).
    /// </summary>
    private void ReviveFromDeath(Entity<SlasherRegenerateComponent> ent)
    {
        var (uid, comp) = ent;

        var filter = Filter.Empty().AddInRange(_transform.GetMapCoordinates(uid), comp.RegenerateEffectRange);
        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } viewer)
                EnsureComp<SlasherRegenerateOverlayComponent>(viewer);
        }

        var revived = new SlasherRevivedFromDeathEvent();
        RaiseLocalEvent(uid, ref revived);
    }

    private void TryInjectReagent(EntityUid target, SlasherRegenerateComponent comp)
    {
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        if (!_solutions.ResolveSolution(target, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution))
            return;

        _solutions.TryAddReagent(bloodstream.BloodSolution.Value, new ReagentId(comp.Reagent, null), FixedPoint2.New(comp.ReagentAmount), out _);
    }

    /// <summary>
    /// Grants a soul to use for regenerate. Called when the slasher successfully steals a soul in soulsteal.
    /// </summary>
    public void GrantSoul(EntityUid uid, SlasherRegenerateComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.HasSoulAvailable = true;
        Dirty(uid, comp);
    }
}
