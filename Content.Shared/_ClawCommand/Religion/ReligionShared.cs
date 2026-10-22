// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Religion;

/// <summary>
/// Held (or worn in <see cref="ValidSpellDenialSlots"/>) items with this protect their holder from unholy touch powers,
/// such as the devil's grip.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DivineInterventionComponent : Component
{
    /// <summary>
    /// Which sound to play on spell denial.
    /// </summary>
    [DataField]
    public SoundSpecifier DenialSound = new SoundPathSpecifier("/Audio/Effects/hallelujah.ogg");

    /// <summary>
    /// Which effect to display.
    /// </summary>
    [DataField]
    public EntProtoId EffectProto = "EffectSparks";

    /// <summary>
    /// Which loc string to display.
    /// </summary>
    [DataField]
    public LocId DenialString = "nullrod-spelldenial-popup";

    /// <summary>
    /// Valid inventory slots for spell denial when equipped
    /// </summary>
    [DataField]
    public SlotFlags ValidSpellDenialSlots = SlotFlags.NONE;
}

/// <summary>
/// A chaplain banishing an incapacitated devil back to hell with a bible.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ExorcismDoAfterEvent : SimpleDoAfterEvent;
