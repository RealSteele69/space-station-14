// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;

namespace Content.Shared._GoobStation.Wraith.Actions;

/// <summary>
/// Increments/Decrements the use delay of an action
/// </summary>
public sealed partial class ActionUseDelayOnUseSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    private EntityQuery<ActionComponent> _actionQuery;

    public override void Initialize()
    {
        base.Initialize();

        _actionQuery = GetEntityQuery<ActionComponent>();

        SubscribeLocalEvent<ActionUseDelayOnUseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ActionUseDelayOnUseComponent, ActionPerformedEvent>(OnActionPerformed);
    }

    private void OnMapInit(Entity<ActionUseDelayOnUseComponent> ent, ref MapInitEvent args)
    {
        if (!_actionQuery.TryComp(ent, out var action)
            || action.UseDelay == null)
            return;

        ent.Comp.OriginalUseDelay = action.UseDelay.Value;
    }
    private void OnActionPerformed(Entity<ActionUseDelayOnUseComponent> ent, ref ActionPerformedEvent args)
    {
        if (!_actionQuery.TryComp(ent, out var action))
            return;

        var newUseDelay = action.UseDelay + ent.Comp.UseDelayAccumulator;
        _actions.SetUseDelay(ent.Owner, newUseDelay);
    }
}
