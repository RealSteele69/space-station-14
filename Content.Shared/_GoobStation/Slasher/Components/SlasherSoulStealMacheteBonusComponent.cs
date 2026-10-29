// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Applied to the machete to provide cumulative soul steal bonuses.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlasherSoulStealMacheteBonusComponent : Component
{
    [DataField]
    public float SlashBonus;
}
