// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._GoobStation.SlaughterDemon;

/// <summary>
/// Triggers once an entity devours another entity
/// </summary>
[ByRefEvent]
public record struct SlaughterDevourEvent(
    EntityUid pullingEnt,
    EntityUid pullerEnt);

/// <summary>
///  Raised on the entity that gets devoured
/// </summary>
/// <param name="Devoured"></param>
/// <param name="Devourer"></param>
[ByRefEvent]
public record struct SlaughterDevourAttemptEvent(
    EntityUid Devoured,
    EntityUid Devourer,
    bool Handled = false,
    bool Cancelled = false);

/// <summary>
/// Doafter for when an entity attempts to devour an entity
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SlaughterDevourDoAfter : SimpleDoAfterEvent;
