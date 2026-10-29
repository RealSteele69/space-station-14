// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.SlaughterDemon;

/// <summary>
/// This is used for marking an entity as able to devour people with blood crawl
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SlaughterDevourComponent : Component
{
    /// <summary>
    /// Devouring doafter
    /// </summary>
    [DataField(required: true)]
    public float DoAfterDelay;

    /// <summary>
    /// Healing done when eating someone
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier ToHeal = new();

    /// <summary>
    /// Healing done when eating a robot
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier ToHealNonCrew = new();

    /// <summary>
    /// Healing done when eating anything else
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier ToHealAnythingElse = new();

    /// <summary>
    /// The sound that plays once devouring someone
    /// </summary>
    [DataField]
    public SoundSpecifier? FeastSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg")
    {
        Params = AudioParams.Default.WithVolume(-3f),
    };

    /// <summary>
    /// A container that holds the entities instead of outright removing them
    /// </summary>
    [DataField]
    public Container? Container;
}
