// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Other;

[RegisterComponent, NetworkedComponent]
public sealed partial class WraithWeakenedComponent : Component;

[ByRefEvent]
public record struct WraithWeakenedAddedEvent;

[ByRefEvent]
public record struct WraithWeakenedRemovedEvent;
