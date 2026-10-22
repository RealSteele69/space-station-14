// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Supermatter.Components;

/// <summary>
///     Feeds the supermatter this much power when it collides with it, instead of the default 1.
/// </summary>
[RegisterComponent]
public sealed partial class SupermatterFoodComponent : Component
{
    [DataField]
    public int Energy = 1;
}
