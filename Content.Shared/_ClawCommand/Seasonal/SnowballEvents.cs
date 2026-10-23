using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Seasonal;

/// <summary>
/// Scooping a snowball off a snowy tile.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SnowScoopDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Packing a loose snowball into a hard one.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SnowballPackDoAfterEvent : SimpleDoAfterEvent;
