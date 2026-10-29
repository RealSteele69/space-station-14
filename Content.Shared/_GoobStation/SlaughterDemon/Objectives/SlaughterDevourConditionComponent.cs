// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

namespace Content.Shared._GoobStation.SlaughterDemon.Objectives;

[RegisterComponent]
public sealed partial class SlaughterDevourConditionComponent : Component
{
    /// <summary>
    /// The amount of devoured crewmembers required
    /// </summary>
    [DataField]
    public int Devour;
}
