// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Grants the Slasher the ability to summon (or create) their machete into their active hand. Very generic.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlasherSummonMacheteComponent : Component
{
    [ViewVariables]
    public EntityUid? ActionEnt;

    [DataField]
    public EntProtoId ActionId = "ActionSlasherSummonMachete";

    /// <summary>
    /// Prototype id of the item to summon.
    /// </summary>
    [DataField]
    public EntProtoId MachetePrototype = "SlasherMachete";

    /// <summary>
    /// The current tracked machete entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? MacheteUid;
}
