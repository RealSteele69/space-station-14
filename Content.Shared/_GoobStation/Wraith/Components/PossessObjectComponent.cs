// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PossessObjectComponent : Component
{
    [DataField]
    public SoundSpecifier? PossessSound = new SoundPathSpecifier("/Audio/_GoobStation/Wraith/wraithlivingobject.ogg");

    [DataField]
    public TimeSpan PossessDuration = TimeSpan.FromSeconds(30f);
}
