// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// Progress is 1 once the vampire has claimed a coffin as their lair, 0 otherwise.
/// </summary>
[RegisterComponent]
public sealed partial class VampireLairConditionComponent : Component;

/// <summary>
/// Progress is 1 once the vampire has at least one bound Vassal.
/// </summary>
[RegisterComponent]
public sealed partial class VampireProtegeConditionComponent : Component;

/// <summary>
/// Progress scales with how many distinct victims the vampire has fed on, out of <see cref="Goal"/>.
/// </summary>
[RegisterComponent]
public sealed partial class VampireBloodHarvestConditionComponent : Component
{
    [DataField]
    public int Goal = 3;
}
