// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Slasher;
using Content.Shared._GoobStation.Slasher.Components;

namespace Content.Server._GoobStation.Slasher.Systems;

/// <summary>
/// Server-side of the slasher Regenerate system: relocates the slasher to a locker when they
/// come back from death.
/// </summary>
public sealed partial class ServerSlasherRegenerateSystem : EntitySystem
{
    [Dependency] private AntagLockerSpawnSystem _lockerSpawn = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherRegenerateComponent, SlasherRevivedFromDeathEvent>(OnRevivedFromDeath);
    }

    private void OnRevivedFromDeath(Entity<SlasherRegenerateComponent> ent, ref SlasherRevivedFromDeathEvent args)
    {
        _lockerSpawn.TryRelocateToLocker(ent.Owner);
    }
}
