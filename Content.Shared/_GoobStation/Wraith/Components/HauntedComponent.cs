// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HauntedComponent : Component
{
    /// <summary>
    /// How long the Haunted component lasts until it deletes itself.
    /// </summary>
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The deletion time for the component, set automatically on map init.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DeletionTime = TimeSpan.Zero;
}
