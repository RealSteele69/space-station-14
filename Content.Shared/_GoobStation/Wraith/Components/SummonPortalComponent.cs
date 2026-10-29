// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class SummonPortalComponent : Component
{
    /// <summary>
    /// How many portals are currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentActivePortals;

    /// <summary>
    /// The currently active portal spawned by this entity, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? CurrentPortal;

    /// <summary>
    /// How many portals the wraith is allowed to have.
    /// </summary>
    [DataField]
    public int PortalLimit = 1;

    /// <summary>
    /// The prototype ID for the void portal ritual circle.
    /// </summary>
    [DataField]
    public EntProtoId RitualCircle = "RitualCircle4";
}
