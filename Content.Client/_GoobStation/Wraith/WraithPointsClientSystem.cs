// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.WraithPoints;
using Content.Shared.Alert.Components;

namespace Content.Client._GoobStation.Wraith;

public sealed partial class WraithPointsClientSystem : EntitySystem
{
    [Dependency] private WraithPointsSystem _wraithPointsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WraithPointsComponent, GetGenericAlertCounterAmountEvent>(OnGenericCounterAlert);
    }

    private void OnGenericCounterAlert(Entity<WraithPointsComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled
            || ent.Comp.Alert != args.Alert)
            return;

        args.Amount = _wraithPointsSystem.GetCurrentWp(ent.Owner).Int();
    }
}
