// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Supermatter.Components;

/// <summary>
///     The supermatter will not consume this entity, and lets it cut a sliver off the crystal.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SupermatterImmuneComponent : Component;
