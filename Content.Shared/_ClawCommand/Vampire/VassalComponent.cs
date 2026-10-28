// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// A thrall bound to a vampire master: loyal (and loyalty-bonded) to them, freed the moment the master
/// meets their final death.
/// </summary>
[RegisterComponent]
public sealed partial class VassalComponent : Component
{
    [DataField(required: true)]
    public EntityUid Master;

    /// <summary>
    /// The "keep your master alive" objective entity granted on conversion, so it can be cleaned up
    /// when the bond breaks.
    /// </summary>
    [DataField]
    public EntityUid? Objective;

    /// <summary>
    /// Actions granted on conversion, so they can be stripped again if the bond breaks.
    /// </summary>
    [DataField]
    public List<EntityUid> ActionEntities = new();
}

/// <summary>
/// Marks a vassal as actively using Recuperate - healing on their master's borrowed blood, at the cost
/// of their own.
/// </summary>
[RegisterComponent]
public sealed partial class VassalRecuperatingComponent : Component
{
    [DataField]
    public TimeSpan NextTick;

    [DataField]
    public EntityUid Action;
}

/// <summary>
/// Raised on a potential vassal before converting them, so anything that should block the conversion
/// (an existing antag role, a special immunity, etc.) gets a chance to cancel it.
/// </summary>
[ByRefEvent]
public record struct AttemptConvertVassalEvent(bool Cancelled);
