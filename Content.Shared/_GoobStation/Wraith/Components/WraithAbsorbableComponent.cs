// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class WraithAbsorbableComponent : Component
{
    /// <summary>
    ///  Whether the user has been absorbed
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Absorbed;
}
