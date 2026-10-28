// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// A wooden stake. Driven through a helpless vampire's heart, it ends them for good; driven through a
/// conscious one, it just locks their powers away until it's pulled free.
/// </summary>
[RegisterComponent]
public sealed partial class VampireStakeComponent : Component
{
    [DataField]
    public TimeSpan StakeDuration = TimeSpan.FromSeconds(10);

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
