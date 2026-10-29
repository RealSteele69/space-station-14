// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Spook;

[RegisterComponent, NetworkedComponent]
public sealed partial class CreateEctoplasmComponent : Component
{
    [DataField]
    public int SearchRange = 10;

    [DataField]
    public EntProtoId EctoplasmProto = "Ectoplasm";

    /// <summary>
    /// Minimum and Maximum amounts of ectoplasm to spawn
    /// </summary>
    [DataField]
    public Vector2i AmountMinMax = new(3, 6);
}
