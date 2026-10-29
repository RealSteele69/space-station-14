// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Components.Mobs;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CloakComponent : Component
{
    /// <summary>
    /// How long the buff lasts before expiring.
    /// </summary>
    [DataField]
    public TimeSpan CloakDuration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// If true, cloak is currently active and ticking down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsActive;

    /// <summary>
    /// The timestamp when the cloak effect should end.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;
}
