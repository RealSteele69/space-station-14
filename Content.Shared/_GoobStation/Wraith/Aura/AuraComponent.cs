// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Aura;

/// <summary>
/// Creates an aura around you.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class AuraComponent : Component
{
    /// <summary>
    /// The intensity of the aura
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AuraFarm = 0.5f;

    [DataField, AutoNetworkedField]
    public Color AuraColor = Color.Black;

    /// <summary>
    /// How much to distort the aura
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Distortion = 0.05f;
}
