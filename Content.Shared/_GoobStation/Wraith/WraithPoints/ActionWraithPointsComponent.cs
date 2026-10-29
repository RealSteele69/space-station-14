// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.WraithPoints;

[RegisterComponent, NetworkedComponent]
public sealed partial class ActionWraithPointsComponent : Component
{
    /// <summary>
    /// The amount of WP to reduce from the entity on action use
    /// </summary>
    [DataField(required: true)]
    public int WpConsume;

    [DataField]
    public LocId Popup = "wraith-action-generic-fail";
}
