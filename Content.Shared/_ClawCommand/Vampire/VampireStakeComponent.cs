// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// A stake. Only goes in while the target is lying down. Driven through the heart of a vampire who is
/// asleep, unconscious, in torpor or out of blood, it ends them for good; a conscious one just has their
/// powers locked away for a while. Better stakes go in faster and hold longer.
/// </summary>
[RegisterComponent]
public sealed partial class VampireStakeComponent : Component
{
    [DataField]
    public TimeSpan StakeDuration = TimeSpan.FromSeconds(12);

    [DataField]
    public DamageSpecifier FinalDeathDamage = new()
    {
        DamageDict = { ["Blunt"] = 1000 },
    };

    /// <summary>
    /// Multiplies how long a conscious vampire's powers stay locked away once staked with this.
    /// </summary>
    [DataField]
    public float StakedDurationMultiplier = 1f;
}

/// <summary>
/// A vampire with a stake through their heart. Every power but Feed refuses to work while this is
/// present, and it blocks all healing.
/// </summary>
[RegisterComponent]
public sealed partial class VampireStakedComponent : Component
{
    [DataField]
    public TimeSpan ExpiresAt;
}
