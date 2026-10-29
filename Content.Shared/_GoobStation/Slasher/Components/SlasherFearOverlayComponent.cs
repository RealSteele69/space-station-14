// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Default slasher fear overlay.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlasherFearOverlayComponent : Component
{
    [DataField]
    public float FadeSpeed = 1.5f;

    [DataField]
    public float BaseIntensity = 0.15f;
}
