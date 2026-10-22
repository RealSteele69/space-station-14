// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Events;
using Robust.Shared.Map;

namespace Content.Shared._ClawCommand.Lavaland.Megafauna.Components;

/// <summary>
/// Holds the projectile volleys a boss still has to fire.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaProjectileBurstComponent : Component
{
    [ViewVariables]
    public List<MegafaunaProjectileBurst> Bursts = new();
}

public sealed class MegafaunaProjectileBurst
{
    public MegafaunaProjectileActionEvent Args = default!;
    public EntityUid? TargetEntity;
    public MapCoordinates TargetCoords;
    public int VolleysLeft;
    public int VolleyIndex;
    public TimeSpan NextVolley;
}

/// <summary>
/// Present on a boss while it winds up or performs a charge.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaChargingComponent : Component
{
    [ViewVariables]
    public MegafaunaChargeActionEvent Args = default!;

    [ViewVariables]
    public EntityUid? TargetEntity;

    [ViewVariables]
    public MapCoordinates TargetCoords;

    [ViewVariables]
    public int ChargesLeft;

    /// <summary>
    /// When the current wind-up ends and the dash begins. Null while dashing.
    /// </summary>
    [ViewVariables]
    public TimeSpan? DashStart;

    [ViewVariables]
    public Vector2 Direction;

    [ViewVariables]
    public float DistanceLeft;

    [ViewVariables]
    public Vector2i? LastTrailTile;

    [ViewVariables]
    public HashSet<EntityUid> Hit = new();

    /// <summary>
    /// Whether the NPC was awake before the charge started, so it can be woken back up.
    /// </summary>
    [ViewVariables]
    public bool WasAwake;
}

/// <summary>
/// Present on a boss while it is in the air during a swoop.
/// Blocks all damage and hides the boss.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaAirborneComponent : Component
{
    [ViewVariables]
    public MegafaunaSwoopActionEvent Args = default!;

    [ViewVariables]
    public EntityUid? TargetEntity;

    [ViewVariables]
    public EntityUid? Shadow;

    [ViewVariables]
    public TimeSpan TrackEnd;

    [ViewVariables]
    public TimeSpan LandTime;

    [ViewVariables]
    public bool WasAwake;
}

/// <summary>
/// Makes the boss treat every living player it can see within range as an aggressor,
/// so it starts fighting as soon as someone walks into its lair.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaAggroRadiusComponent : Component
{
    [DataField]
    public float Radius = 8f;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextCheck;
}
