// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class FadingInComponent : Component
{
    /// <summary>
    /// Time in seconds for the sprite to fade in from 0 to 1
    /// </summary>
    [DataField]
    public float FadeInTime = 1f;

    /// <summary>
    /// Tracks elapsed time since spawn
    /// </summary>
    [ViewVariables]
    public float Elapsed;

    /// <summary>
    /// True after the sprite has fully faded in
    /// </summary>
    [ViewVariables]
    public bool Finished => Elapsed >= FadeInTime;
}
