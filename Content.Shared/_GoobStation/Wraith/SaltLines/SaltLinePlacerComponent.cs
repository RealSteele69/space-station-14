// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.SaltLines;

[RegisterComponent, NetworkedComponent]
public sealed partial class SaltLinePlacerComponent : Component
{
    [DataField]
    public EntProtoId SaltLine = "WraithSaltLine";
}
