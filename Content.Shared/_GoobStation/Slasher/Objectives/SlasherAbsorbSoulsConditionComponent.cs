// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Slasher.Objectives;

/// <summary>
/// Objective condition: absorb a number of souls.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlasherAbsorbSoulsConditionComponent : Component
{
    /// <summary>
    /// Souls absorbed so far for this objective.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Absorbed;
}
