// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source force-set the grab straight to "hard grab" via a grabStageOverride param on
// TryStartPull that doesn't exist here. This fork already has its own escalating grab ladder
// (Content.Shared.Movement.Pulling.Systems.PullingSystem.Grab.cs, ported from the space fork) -
// pulling someone already in combat mode escalates the grab a step each time the pull key is
// pressed again. So "Relentless Grab" here instantly starts the pull (skipping the normal pull
// key press) and lets the player escalate it to a hard grab themselves afterwards, same as any
// other pull.

using System.Linq;
using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.Actions;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._GoobStation.Slasher.Systems;

/// <summary>
/// Handles the Slasher Relentless Grab action.
/// When activated, the slasher's next melee hit will grab the target.
/// </summary>
public sealed partial class SlasherRelentlessGrabSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherRelentlessGrabComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlasherRelentlessGrabComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SlasherRelentlessGrabComponent, SlasherRelentlessGrabEvent>(OnActivate);
        SubscribeLocalEvent<SlasherRelentlessGrabComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMapInit(Entity<SlasherRelentlessGrabComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEnt, ent.Comp.ActionId);
    }

    private void OnShutdown(Entity<SlasherRelentlessGrabComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEnt);
    }

    private void OnActivate(Entity<SlasherRelentlessGrabComponent> ent, ref SlasherRelentlessGrabEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Ready = true;
        Dirty(ent);

        _popup.PopupPredicted(Loc.GetString("slasher-relentless-grab-activate"), ent.Owner, ent.Owner);

        args.Handled = true;
    }

    private void OnMeleeHit(Entity<SlasherRelentlessGrabComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.Ready || args.HitEntities.Count == 0)
            return;

        var target = args.HitEntities.First();
        if (!_pulling.CanPull(ent.Owner, target))
            return;

        if (!_pulling.TryStartPull(ent.Owner, target))
            return;

        ent.Comp.Ready = false;
        Dirty(ent);
    }
}
