// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// Marks a vampire as using Cloak of Darkness. Broken by taking any damage.
/// </summary>
[RegisterComponent]
public sealed partial class VampireCloakComponent : Component
{
    /// <summary>
    /// The Cloak action entity, so it can be un-toggled when the cloak breaks.
    /// </summary>
    [DataField(required: true)]
    public EntityUid Action;
}
