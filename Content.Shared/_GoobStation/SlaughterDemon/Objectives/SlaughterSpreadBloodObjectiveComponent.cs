// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

namespace Content.Shared._GoobStation.SlaughterDemon.Objectives;

[RegisterComponent]
public sealed partial class SlaughterSpreadBloodObjectiveComponent : Component
{
    [DataField]
    public List<string> Areas = new()
    {
        "Brig",
        "Chapel",
        "Bridge"
    };

    [DataField]
    public string? Title;
}
