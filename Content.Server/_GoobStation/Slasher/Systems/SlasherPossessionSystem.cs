// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: this fork's Content.Server._ClawCommand.Possession.PossessionSystem (ported earlier
// for the Devil antagonist) has a different TryPossessTarget signature than Goobstation's -
// doesMindshieldBlock is a named parameter here instead of a separate check beforehand, there's no
// "hideActions" parameter, and PossessedComponent here has no HiddenActions list to unhide (so the
// UnHideActions call after possessing was dropped - nothing to unhide).

using Content.Server._ClawCommand.Possession;
using Content.Shared._GoobStation.Slasher;
using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.Actions;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;

namespace Content.Server._GoobStation.Slasher.Systems;

public sealed partial class SlasherPossessionSystem : EntitySystem
{
    [Dependency] private PossessionSystem _possession = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherPossessionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlasherPossessionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SlasherPossessionComponent, SlasherPossessionEvent>(OnPossess);
    }

    private void OnMapInit(Entity<SlasherPossessionComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEnt, ent.Comp.ActionId);
    }

    private void OnShutdown(Entity<SlasherPossessionComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEnt);
    }

    /// <summary>
    /// Handles the possession of a target.
    /// </summary>
    private void OnPossess(Entity<SlasherPossessionComponent> ent, ref SlasherPossessionEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<MobStateComponent>(args.Target))
            return;

        _possession.TryPossessTarget(args.Target,
            ent.Owner,
            ent.Comp.PossessionDuration,
            pacifyPossessed: false,
            doesMindshieldBlock: ent.Comp.DoesMindshieldBlock,
            polymorphPossessor: true);

        args.Handled = true;
    }
}
