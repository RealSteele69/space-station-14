// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Makes the entity's sprite fade out in darkness and back in under light.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BoogeymanShadowComponent : Component;
