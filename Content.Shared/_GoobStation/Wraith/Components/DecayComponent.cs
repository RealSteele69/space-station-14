// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Emag.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DecayComponent : Component
{
    /// <summary>
    /// How much stamina damage to apply over time.
    /// </summary>
    [DataField]
    public float StaminaDamageAmount = 150f;

    /// <summary>
    /// What emag interaction to use
    /// </summary>
    [DataField]
    public EmagType Emag = EmagType.All;
}
