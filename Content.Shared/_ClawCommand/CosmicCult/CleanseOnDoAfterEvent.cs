using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.CosmicCult;

[Serializable, NetSerializable]
public sealed partial class CleanseOnDoAfterEvent : SimpleDoAfterEvent;
