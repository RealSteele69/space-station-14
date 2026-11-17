// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Religion.Nullrod;

/// <summary>
/// A chaplain's holy weapon. Can be bound to a chaplain, recalled at an altar, and transformed on altars
/// with an <see cref="AltarSourceComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NullrodComponent : Component
{
    /// <summary>
    /// Whether non bible-users are prevented from attacking or shooting with this.
    /// </summary>
    [DataField]
    public bool UntrainedUseRestriction;

    /// <summary>
    /// How much damage is dealt when an untrained user tries to use it.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier DamageOnUntrainedUse = default!;

    /// <summary>
    /// Pop-up shown to an untrained user.
    /// </summary>
    [DataField(required: true)]
    public LocId UntrainedUseString;

    /// <summary>
    /// Sound played on untrained use.
    /// </summary>
    [DataField]
    public SoundSpecifier UntrainedUseSound = new SoundPathSpecifier("/Audio/Effects/hallelujah.ogg");

    /// <summary>
    /// Attack attempts fire every swing, so the punishment has a cooldown.
    /// </summary>
    [DataField]
    public TimeSpan PopupCooldown = TimeSpan.FromSeconds(3.0);

    [DataField, AutoNetworkedField]
    public TimeSpan? NextPopupTime;

    /// <summary>
    /// How this null rod is brought back when recalled at an altar.
    /// </summary>
    [DataField]
    public NullrodRecallType RecallType = NullrodRecallType.None;
}

[Serializable, NetSerializable]
public enum NullrodRecallType : byte
{
    /// <summary>Can't be recalled.</summary>
    None,
    /// <summary>Picked up into a free hand.</summary>
    Normal,
    /// <summary>Unremoveable from the hand, e.g. the Hand of God.</summary>
    Unremoveable,
    /// <summary>Detached from whatever it's stuck in first, e.g. the ancient spear.</summary>
    Embedded,
    /// <summary>Brings its sister weapon along too, e.g. Casul and Jackal.</summary>
    DualWield,
}
