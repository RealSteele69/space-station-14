// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class WraithCommandComponent : Component
{
    /// <summary>
    /// The search range of nearby objects
    /// </summary>
    [DataField(required: true)]
    public float SearchRange = 5f;

    /// <summary>
    ///  What objects are blacklisted from being hurled
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist Blacklist = new();

    /// <summary>
    /// The throw speed in which to throw the objects
    /// </summary>
    [DataField]
    public float ThrowSpeed = 30f;

    [DataField(required: true)]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(0.5f);
}
