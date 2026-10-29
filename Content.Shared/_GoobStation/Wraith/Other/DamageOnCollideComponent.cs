// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Other;

/// <summary>
/// Will damage the entity with DamageOnCollideComponent upon colliding with any entity from the whitelist
/// (if empty will damage on any collision)
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DamageOnCollideComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public EntityWhitelist? IgnoreWhitelist = new();

    [DataField]
    public EntityWhitelist? Whitelist = new();

    /// <summary>
    /// If true, the damage is applied to the entity colliding with us (args.OtherEntity)
    /// If false (default), the damage is applied to us (ent.Owner)
    /// </summary>
    [DataField]
    public bool Inverted;
}
