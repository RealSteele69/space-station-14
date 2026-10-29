// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Revenant;

[RegisterComponent, NetworkedComponent]
public sealed partial class RevenantCrushComponent : Component
{
    [DataField]
    public SoundSpecifier? CrushSound = new SoundPathSpecifier("/Audio/_GoobStation/Wraith/revtouch.ogg");

    /// <summary>
    ///  The duration of the doafter
    /// </summary>
    [DataField(required: true)]
    public TimeSpan AbilityDuration = TimeSpan.FromSeconds(8);

    /// <summary>
    ///  How much damage to deal to the entity before the doafter starts
    /// </summary>
    [DataField]
    public DamageSpecifier? InitialDamage = new();

    /// <summary>
    ///  How long to knockdown the user for
    /// </summary>
    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(2f);

    /// <summary>
    ///  Allowed distance for doafter between user and target
    /// </summary>
    [DataField]
    public float Distance = 15f;
}
