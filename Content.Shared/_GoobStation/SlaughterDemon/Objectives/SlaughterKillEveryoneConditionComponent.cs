// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

namespace Content.Shared._GoobStation.SlaughterDemon.Objectives;

/// <summary>
/// This is used for the objective which is for devouring everyone on the station
/// </summary>
[RegisterComponent]
public sealed partial class SlaughterKillEveryoneConditionComponent : Component
{
    [DataField]
    public string? Title;

    [DataField]
    public string? Description;

    [DataField]
    public int Devoured;
}
