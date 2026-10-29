// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: see DefileComponent.cs - replaced the Content.Shared._White.ListViewSelector freeform UI
// with the shared WraithRadialEntry/SimpleRadialMenu plumbing, picking from the fixed Reagents dictionary
// instead of typing an amount.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared._GoobStation.Wraith.UI;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared._GoobStation.Wraith.Systems;

public sealed partial class DefileSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DefileComponent, DefileEvent>(OnDefile);
        SubscribeLocalEvent<DefileComponent, WraithRadialSelectedMessage>(OnDefileSelected);
    }

    private void OnDefile(Entity<DefileComponent> ent, ref DefileEvent args)
    {
        if (args.Target == ent.Owner)
        {
            var entries = new List<WraithRadialEntry>();
            foreach (var reagent in ent.Comp.Reagents.Keys)
                entries.Add(new WraithRadialEntry(reagent.Id, reagent.Id));

            _ui.SetUiState(ent.Owner, WraithDefileUiKey.Key, new WraithRadialBoundUserInterfaceState(entries));
            _ui.TryToggleUi(ent.Owner, WraithDefileUiKey.Key, args.Performer);
        }
        else
        {
            if (!TryInjectReagents(args.Target, ent))
                return;

            _popup.PopupClient(Loc.GetString("wraith-poison-success", ("target", ent.Owner)), ent.Owner, ent.Owner);
            args.Handled = true;
        }
    }

    private void OnDefileSelected(Entity<DefileComponent> ent, ref WraithRadialSelectedMessage args)
    {
        if (!ent.Comp.Reagents.TryGetValue(args.SelectedId, out var amount))
            return;

        ent.Comp.ReagentSelected = args.SelectedId;
        ent.Comp.ReagentSelectedAmount = amount;
        Dirty(ent);

        _ui.CloseUi(ent.Owner, WraithDefileUiKey.Key);
    }


    #region Helper
    private bool TryInjectReagents(EntityUid target, Entity<DefileComponent> ent)
    {
        if (!ent.Comp.ReagentSelected.HasValue)
            return false;

        var solution = new Solution();
        solution.AddReagent(ent.Comp.ReagentSelected.Value, ent.Comp.ReagentSelectedAmount);

        if (!_solution.TryGetSolution(target, "drink", out var targetSolution) &&
            !_solution.TryGetSolution(target, "food", out targetSolution))
            return false;

        if (!TryComp<SolutionComponent>(targetSolution.Value, out var solComp))
            return false;

        // Ensure capacity is large enough before injecting
        var needed = solComp.Solution.Volume + solution.Volume;
        if (needed > solComp.Solution.MaxVolume)
        {
            solComp.Solution.MaxVolume = needed;
            Dirty(targetSolution.Value, solComp);
        }

        return _solution.TryAddSolution(targetSolution.Value, solution);
    }
    #endregion
}
