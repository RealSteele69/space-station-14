// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

namespace Content.Shared._GoobStation.Wraith.SaltLines;

[RegisterComponent]
public sealed partial class SaltLineVisualizerComponent : Component
{
    [DataField]
    public string State = "salt_";
}
