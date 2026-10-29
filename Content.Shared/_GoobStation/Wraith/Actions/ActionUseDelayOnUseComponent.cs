// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Actions;

[RegisterComponent, NetworkedComponent]
public sealed partial class ActionUseDelayOnUseComponent : Component
{
    /// <summary>
    /// How much use delay to add per action use
    /// </summary>
    [DataField(required: true)]
    public TimeSpan UseDelayAccumulator;

    /// <summary>
    /// The original use delay
    /// </summary>
    [ViewVariables]
    public TimeSpan OriginalUseDelay;
}
