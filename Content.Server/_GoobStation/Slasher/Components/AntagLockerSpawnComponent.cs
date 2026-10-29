// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.Map;

namespace Content.Server._GoobStation.Slasher.Components;

/// <summary>
/// When added to a game rule alongside AntagSelectionComponent,
/// places the ghost-role antag spawner inside a random station locker instead of an open tile.
/// Falls back to a safe random tile if none are found.
/// </summary>
[RegisterComponent]
public sealed partial class AntagLockerSpawnComponent : Component
{
    /// <summary>
    /// When true, only lockers tagged MaintenanceCloset are eligible.
    /// </summary>
    [DataField]
    public bool MaintenanceOnly = true;

    [DataField]
    public EntityUid? ChosenLocker;

    /// <summary>
    /// Set when no eligible locker is found. Uses AntagBetterRandomSpawnSystem as a fallback.
    /// </summary>
    [DataField]
    public EntityCoordinates? FallbackCoords;

    [DataField]
    public bool Placed;
}
