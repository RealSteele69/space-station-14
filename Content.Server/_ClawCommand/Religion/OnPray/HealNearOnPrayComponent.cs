// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Religion.OnPray;

/// <summary>
/// Praying to this heals everyone nearby and burns anyone who can take holy damage.
/// </summary>
[RegisterComponent]
public sealed partial class HealNearOnPrayComponent : Component
{
    [DataField]
    public DamageSpecifier Healing = new();

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public SoundSpecifier HealSoundPath = new SoundPathSpecifier("/Audio/Effects/holy.ogg");

    [DataField]
    public SoundSpecifier SizzleSoundPath = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");

    [DataField]
    public EntProtoId HealEffect = "EffectSparks";

    [DataField]
    public EntProtoId DamageEffect = "WeaponArcSlash";

    [DataField]
    public float Range = 5f;
}
