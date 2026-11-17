// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;

namespace Content.Server._ClawCommand.Religion.OnPray;

/// <summary>
/// Praying to this gun refills its basic entity ammo.
/// </summary>
[RegisterComponent]
public sealed partial class ReloadOnPrayComponent : Component
{
    [DataField]
    public SoundSpecifier ReloadSoundPath = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/shotgun_insert.ogg");
}
