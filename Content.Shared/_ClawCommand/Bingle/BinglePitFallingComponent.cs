// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._ClawCommand.Bingle;

/// <summary>
/// Added to something while it falls into a bingle pit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class BinglePitFallingComponent : Component
{
    /// <summary>
    /// Time it takes for the falling animation (scaling down) to complete.
    /// </summary>
    [DataField]
    public TimeSpan AnimationTime = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    /// Time until the entity is actually moved into the pit.
    /// </summary>
    [DataField]
    public TimeSpan DeletionTime = TimeSpan.FromSeconds(1.8f);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextDeletionTime = TimeSpan.Zero;

    /// <summary>
    /// Original scale of the sprite, restored if the component is removed mid-animation.
    /// </summary>
    [ViewVariables]
    public Vector2 OriginalScale = Vector2.Zero;

    [ViewVariables]
    public Vector2 AnimationScale = new(0.01f, 0.01f);

    [ViewVariables]
    public EntityUid? Pit;
}
