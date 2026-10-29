// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Minions.Harbinger;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpikerLashComponent : Component
{
    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float BleedAmount = 25f;

    [DataField]
    public SoundSpecifier? LashSound = new SoundPathSpecifier("/Audio/_GoobStation/Wraith/Attack/Flesh_Stab_1.ogg");
}
