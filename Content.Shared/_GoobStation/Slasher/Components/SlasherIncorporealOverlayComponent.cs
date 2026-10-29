// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Gives a vhs-static overlay to any entity that has it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlasherIncorporealOverlayComponent : Component
{
    [DataField]
    public float FadeSpeed = 2.2f;
}
