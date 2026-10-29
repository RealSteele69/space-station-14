// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components.Mobs;
using Content.Shared._GoobStation.Wraith.Events;

namespace Content.Server._GoobStation.Wraith.Systems.Minions;

/// <summary>
/// Spawns a rat den, deletes the previous one if it already exists.
/// </summary>
public sealed class SummonRatDenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SummonRatDenComponent, SummonRatDenEvent>(OnSummonRatDen);
    }

    private void OnSummonRatDen(Entity<SummonRatDenComponent> ent, ref SummonRatDenEvent args)
    {
        if (ent.Comp.RatDenUid == null)
        {
            ent.Comp.RatDenUid = SpawnAtPosition(ent.Comp.RatDen, args.Target);
            args.Handled = true;
            return;
        }

        QueueDel(ent.Comp.RatDenUid); // delete previous rat den
        ent.Comp.RatDenUid = SpawnAtPosition(ent.Comp.RatDen, args.Target); // spawn new one
        args.Handled = true;
    }
}
