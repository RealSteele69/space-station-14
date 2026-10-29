// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Mobs;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Banishment;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class BanishmentComponent : Component
{
    /// <summary>
    ///  Amount of lives the user has left. Doesn't count the starting life.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Lives;

    /// <summary>
    ///  When to trigger the banishment
    /// </summary>
    [DataField]
    public MobState MobStateTrigger = MobState.Dead;

    /// <summary>
    ///  The max lives the user can have
    /// </summary>
    [ViewVariables]
    public int MaxLives;

    [DataField]
    public LocId? Popup;
}

[ByRefEvent]
public record struct BanishmentEvent(int Lives);

/// <summary>
/// Raised when your lives become 0
/// </summary>
[ByRefEvent]
public record struct BanishmentDoneEvent;
