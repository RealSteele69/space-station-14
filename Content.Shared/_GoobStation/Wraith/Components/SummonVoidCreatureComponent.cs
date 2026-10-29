// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SummonVoidCreatureComponent : Component
{
    /// <summary>
    /// The action entity spawned for this component.
    /// </summary>
    [ViewVariables]
    public EntityUid? ActionEnt;

    /// <summary>
    /// Prototype ID for the summon action.
    /// </summary>
    [ViewVariables]
    public EntProtoId ActionId = "ActionSummonVoidCreature";

    /// <summary>
    ///  The ghost entity to summon for the players to take over
    /// </summary>
    [DataField]
    public EntProtoId SummonId = "SummonVoidGhost";
}
