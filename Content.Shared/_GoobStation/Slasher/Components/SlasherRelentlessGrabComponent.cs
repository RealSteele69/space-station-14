// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// When activated, the slasher's next melee hit will grab the target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlasherRelentlessGrabComponent : Component
{
    /// <summary>
    /// The action entity.
    /// </summary>
    [DataField]
    public EntProtoId ActionId = "ActionSlasherRelentlessGrab";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEnt;

    /// <summary>
    /// Whether the grab is ready.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Ready;
}
