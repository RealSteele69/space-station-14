// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Religion.Nullrod;

/// <summary>
/// Raised on an <see cref="AlternatePrayableComponent"/> item once a prayer to it finishes.
/// </summary>
/// <param name="User">The entity praying at the item.</param>
[ByRefEvent]
public record struct AlternatePrayEvent(EntityUid User);

[Serializable, NetSerializable]
public sealed partial class AlternatePrayDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class RecallPrayDoAfterEvent : SimpleDoAfterEvent;
