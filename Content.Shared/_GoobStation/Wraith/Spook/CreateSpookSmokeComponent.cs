// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Chemistry.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Spook;

[RegisterComponent, NetworkedComponent]
public sealed partial class CreateSpookSmokeComponent : Component
{
    [DataField]
    public int SearchRange = 10;

    [DataField]
    public EntProtoId SmokeProto = "Smoke";

    [DataField]
    public Solution SmokeSolution = new();

    /// <summary>
    ///  Duration of the smoke in seconds
    /// </summary>
    [DataField]
    public float Duration = 4f;

    /// <summary>
    /// Spread amount of the smoke
    /// </summary>
    [DataField]
    public int SpreadAmount = 15;

    /// <summary>
    ///  How many smokes to spawn
    /// </summary>
    [DataField]
    public int SmokeAmount = 2;
}
