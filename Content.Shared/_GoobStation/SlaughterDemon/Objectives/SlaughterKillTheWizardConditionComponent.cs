// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

namespace Content.Shared._GoobStation.SlaughterDemon.Objectives;

[RegisterComponent]
public sealed partial class SlaughterKillTheWizardConditionComponent : Component
{
    [DataField]
    public string? Title;

    [DataField]
    public string? Description;
}
