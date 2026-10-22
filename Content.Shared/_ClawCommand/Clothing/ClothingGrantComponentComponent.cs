// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Clothing;

/// <summary>
///     Adds components to whoever wears this clothing in one of its valid slots, and removes them again on unequip.
///     Components the wearer already had are left alone.
/// </summary>
[RegisterComponent, Access(typeof(ClothingGrantingSystem))]
public sealed partial class ClothingGrantComponentComponent : Component
{
    [DataField("component", required: true), AlwaysPushInheritance]
    public ComponentRegistry Components = new();

    /// <summary>
    ///     Names of the components this clothing actually added to its current wearer.
    /// </summary>
    [ViewVariables]
    public HashSet<string> Active = new();
}
