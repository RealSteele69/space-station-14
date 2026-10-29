// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: see SpookComponent.cs - replaced Content.Shared._White.RadialSelector with the shared
// WraithRadialEntry/SimpleRadialMenu plumbing.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.UI;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._GoobStation.Wraith.Systems;

/// <summary>
/// Handles UI opening of the spook menu and activating an action.
/// The actions themselves live in the server-sided SpookActionSystem.
/// </summary>
public sealed partial class SpookSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpookComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<SpookComponent, WraithRadialSelectedMessage>(OnRadialSelected);
    }

    #region UI
    private void OnUIOpenAttempt(Entity<SpookComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<WraithComponent>(args.User))
            args.Cancel();

        _userInterfaceSystem.SetUiState(ent.Owner,
            SpookRadialUiKey.Key,
            new WraithRadialBoundUserInterfaceState(ent.Comp.Actions));
    }

    private void OnRadialSelected(Entity<SpookComponent> ent, ref WraithRadialSelectedMessage args)
    {
        DoSelectedAction(ent.Owner, args.SelectedId);

        _userInterfaceSystem.CloseUi(ent.Owner, SpookRadialUiKey.Key);
    }
    #endregion

    #region Helpers
    private void DoSelectedAction(EntityUid uid, string? action)
    {
        if (action == null
            || !_prototypeManager.TryIndex(action, out var actionProto)
            || !TryComp<ActionsComponent>(uid, out var actions))
            return;

        foreach (var actionEnt in actions.Actions)
        {
            var metadata = MetaData(actionEnt);
            if (metadata.EntityPrototype != actionProto
                || !TryComp<ActionComponent>(actionEnt, out var actionComp)
                || _actions.IsCooldownActive(actionComp, _timing.CurTime))
                continue;

            _actions.PerformAction(uid, (actionEnt, actionComp));
            break;
        }
    }

    #endregion
}
