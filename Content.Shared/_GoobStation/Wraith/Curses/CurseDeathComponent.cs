// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Curses;

[RegisterComponent, NetworkedComponent]
public sealed partial class CurseDeathComponent : Component
{
    [DataField]
    public EntProtoId SmokeProto = "AdminInstantEffectSmoke3";

    [DataField]
    public int TicksElapsed;

    [DataField]
    public float RampMultiplier;

    [DataField]
    public DamageSpecifier BaseDamage = new();

    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Ammonia";

    [DataField]
    public float SmokeDuration = 7f;

    [DataField]
    public int SmokeSpread = 18;

    [DataField]
    public FixedPoint2 WpGeneration = 2;

    [DataField]
    public ProtoId<CursePrototype> Curse = "CurseDeath";

    [DataField]
    public SoundSpecifier? CurseSound1 = new SoundPathSpecifier("/Audio/_GoobStation/Wraith/Ambience/Void_Wail.ogg");

    [DataField]
    public bool MusicIsPlaying;

    [DataField]
    public bool EndIsNigh;

    [DataField]
    public SoundSpecifier? CurseSound2 = new SoundPathSpecifier("/Audio/_GoobStation/Wraith/wraithwhisper2.ogg");
}
