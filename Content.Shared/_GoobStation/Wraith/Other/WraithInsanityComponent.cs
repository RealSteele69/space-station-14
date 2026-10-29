// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Other;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class WraithInsanityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Speed = 1f;

    [DataField, AutoNetworkedField]
    public float Radius = 1f;

    [DataField, AutoNetworkedField]
    public Color Color = Color.Red;
}
