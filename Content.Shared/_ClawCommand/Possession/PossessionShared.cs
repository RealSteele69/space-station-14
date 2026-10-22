// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Possession;

/// <summary>
/// Lets a possessor leave the body before the possession runs out.
/// </summary>
public sealed partial class EndPossessionEarlyEvent : InstantActionEvent;

/// <summary>
/// Marks an entity as immune to possession attempts.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PossessionImmuneComponent : Component;
