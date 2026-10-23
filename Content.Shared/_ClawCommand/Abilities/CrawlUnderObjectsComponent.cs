using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Abilities;

/// <summary>
///     Claw Command - Lets a small mob toggle a sneak mode that takes it underneath tables and other
///     waist-high obstacles. Doors and walls still block it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CrawlUnderObjectsComponent : Component
{
    [DataField]
    public EntityUid? ToggleHideAction;

    [DataField]
    public EntProtoId? ActionProto;

    /// <summary>
    ///     Whether sneak mode is currently on. Networked so shared systems (projectiles, climbing) can predict it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    ///     Fixtures whose collision mask was changed by sneak mode, with their original masks,
    ///     so they can be restored afterwards. Server-side bookkeeping only.
    /// </summary>
    [ViewVariables]
    public List<(string key, int originalMask)> ChangedFixtures = new();

    /// <summary>
    ///     Draw depth the sprite had before sneaking. Client-side only.
    /// </summary>
    [ViewVariables]
    public int? OriginalDrawDepth;

    [DataField]
    public float SneakSpeedModifier = 0.7f;
}

[Serializable, NetSerializable]
public enum SneakMode : byte
{
    Enabled,
}

public sealed partial class ToggleCrawlingStateEvent : InstantActionEvent;

/// <summary>
///     Raised on the mob when its sneak mode turns on or off.
/// </summary>
public sealed class CrawlingUpdatedEvent(bool enabled = false) : EntityEventArgs
{
    public readonly bool Enabled = enabled;
}
