// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Applied to entities who have had their soul stolen. Prevents the slasher from stealing from the same person multiple times.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SoullessComponent : Component
{
    [DataField]
    public ProtoId<StatusIconPrototype> FactionIcon = "SoullessFaction";
}
