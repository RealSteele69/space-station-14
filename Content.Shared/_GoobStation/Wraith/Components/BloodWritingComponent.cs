// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BloodWritingComponent : Component
{
    /// <summary>
    ///  The crayon the user is holding
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? BloodCrayon;

    /// <summary>
    /// The name of the hand slot to put the blood crayon
    /// </summary>
    [ViewVariables]
    public string HandName = "bloodCrayon";

    [ViewVariables]
    public EntProtoId BloodCrayonEntId = "CrayonBlood";
}
