// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Revenant;

[RegisterComponent, NetworkedComponent]
public sealed partial class RevenantPushComponent : Component
{
    [DataField]
    public SoundSpecifier? RevPushSound;

    /// <summary>
    /// Throw speed
    /// </summary>
    [DataField]
    public float ThrowSpeed = 30f;

    /// <summary>
    /// Damage to deal to the target when thrown
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier? DamageWhenThrown = new();
}
