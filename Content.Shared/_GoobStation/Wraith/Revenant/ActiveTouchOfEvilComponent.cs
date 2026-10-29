// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Revenant;

/// <summary>
/// Marks the user as using the ability
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ActiveTouchOfEvilComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public float ThrowSpeed;
}
