// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.SaltLines;

[RegisterComponent, NetworkedComponent]
public sealed partial class ConsumeOnSaltLineComponent : Component
{
    /// <summary>
    /// Amount to consume on use
    /// </summary>
    [DataField]
    public FixedPoint2 Amount = 1f;
}

/// <summary>
/// Raised when attempting to place a salt line
/// </summary>
[ByRefEvent]
public record struct AttemptSaltLineEvent(EntityUid User, bool Cancelled = false);
