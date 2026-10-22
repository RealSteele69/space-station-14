using Content.Shared.DoAfter;
using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Traits;

public sealed partial class EggLayInstantActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class MilkingDoAfterEvent : SimpleDoAfterEvent;
