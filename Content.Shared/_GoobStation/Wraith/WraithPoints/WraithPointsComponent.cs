// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source's FixedPoint2 lives in Content.Goobstation.Maths.FixedPoint, this fork doesn't
// have that namespace - uses the vanilla Content.Shared.FixedPoint.FixedPoint2 instead.

using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.WraithPoints;

[RegisterComponent, NetworkedComponent, Access(typeof(WraithPointsSystem))]
[AutoGenerateComponentState]
public sealed partial class WraithPointsComponent : Component
{
    /// <summary>
    ///  Current wraith points the entity has
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 WraithPoints;

    /// <summary>
    /// How many wraith points the entity starts with
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 StartingWraithPoints;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "WraithPoints";
}
