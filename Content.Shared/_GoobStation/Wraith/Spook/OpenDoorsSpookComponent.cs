// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Spook;

[RegisterComponent, NetworkedComponent]
public sealed partial class OpenDoorsSpookComponent : Component
{
    [DataField]
    public float SearchRadius = 10f;

    [DataField]
    public int MaxContainer = 6;
}
