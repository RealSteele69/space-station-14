// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.Alert.Components;

namespace Content.Client._GoobStation.Slasher.Systems;

/// <summary>
/// Feeds the slasher's and victims' fear meter alert counters.
/// </summary>
public sealed class SlasherFearAlertSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherFearComponent, GetGenericAlertCounterAmountEvent>(OnGetCounter);
        SubscribeLocalEvent<SlasherVictimFearBuildupComponent, GetGenericAlertCounterAmountEvent>(OnGetVictimCounter);
    }

    private void OnGetCounter(Entity<SlasherFearComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled || ent.Comp.Alert != args.Alert)
            return;

        args.Amount = (int) MathF.Round(ent.Comp.CurrentMeter);
    }

    private void OnGetVictimCounter(Entity<SlasherVictimFearBuildupComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled || ent.Comp.Alert != args.Alert)
            return;

        args.Amount = (int) MathF.Round(ent.Comp.Fear * 100f);
    }
}
