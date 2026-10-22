using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Body.Preening;

/// <summary>
///     Claw Command - Marks a feather item whose sprite is tinted by <see cref="FeatherVisuals"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FeatherComponent : Component;

[Serializable, NetSerializable]
public enum FeatherVisuals : byte
{
    FeatherColor,
    BloodColor,
}

[Serializable, NetSerializable]
public enum FeatherVisualLayers : byte
{
    Feather,
    Blood,
}

[Serializable, NetSerializable]
public sealed partial class PreeningDoAfterEvent : SimpleDoAfterEvent;
