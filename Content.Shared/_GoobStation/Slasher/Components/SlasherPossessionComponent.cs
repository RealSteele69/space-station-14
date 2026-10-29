// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Unlocked after stealing enough souls - lets the Slasher temporarily take over a victim's body,
/// using this fork's existing Possession system (ported earlier for the Devil antagonist).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlasherPossessionComponent : Component
{
    [ViewVariables]
    public EntityUid? ActionEnt;

    [DataField]
    public EntProtoId ActionId = "ActionSlasherPossession";

    /// <summary>
    /// How long the possession lasts.
    /// </summary>
    [DataField]
    public TimeSpan PossessionDuration = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Whether a mindshield on the target blocks the possession.
    /// </summary>
    [DataField]
    public bool DoesMindshieldBlock = true;
}
