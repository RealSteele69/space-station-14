// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// Marks a vampire as actively latched onto a grabbed victim, draining their blood each tick.
/// Removed the moment the grab breaks, the victim runs dry, or the vampire toggles Feed off.
/// </summary>
[RegisterComponent]
public sealed partial class VampireFeedingComponent : Component
{
    [DataField(required: true)]
    public EntityUid Target;

    /// <summary>
    /// The Feed action entity, so it can be un-toggled when feeding stops.
    /// </summary>
    [DataField(required: true)]
    public EntityUid Action;
}
