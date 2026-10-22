// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Lavaland.Megafauna.Events;

/// <summary>
/// Fires one or more volleys of projectiles from the boss.
/// A volley is either an arc aimed at the target (<see cref="Spread"/>) or a full ring (<see cref="Radial"/>).
/// Rotating every volley by <see cref="VolleyRotation"/> turns a ring into a spiral.
/// </summary>
public sealed partial class MegafaunaProjectileActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Projectile;

    /// <summary>
    /// Projectiles per volley.
    /// </summary>
    [DataField]
    public int Count = 1;

    /// <summary>
    /// Total arc in degrees the volley is spread over, centered on the target direction.
    /// Ignored when <see cref="Radial"/> is set.
    /// </summary>
    [DataField]
    public float Spread;

    /// <summary>
    /// If true, the projectiles are spread evenly over a full circle.
    /// </summary>
    [DataField]
    public bool Radial;

    /// <summary>
    /// Constant angle offset in degrees added to every volley.
    /// </summary>
    [DataField]
    public float AngleOffset;

    /// <summary>
    /// Extra rotation in degrees applied to each following volley.
    /// </summary>
    [DataField]
    public float VolleyRotation;

    /// <summary>
    /// Random jitter in degrees added to each projectile.
    /// </summary>
    [DataField]
    public float Jitter;

    [DataField]
    public int Volleys = 1;

    /// <summary>
    /// Seconds between volleys.
    /// </summary>
    [DataField]
    public float VolleyDelay = 0.2f;

    /// <summary>
    /// If true, every volley re-aims at the current position of the targeted entity.
    /// </summary>
    [DataField]
    public bool TrackTarget = true;

    [DataField]
    public float ProjectileSpeed = 12f;

    /// <summary>
    /// Played once per volley.
    /// </summary>
    [DataField]
    public SoundSpecifier? VolleySound;

    /// <summary>
    /// Line the boss shouts when it starts this attack.
    /// </summary>
    [DataField]
    public LocId? Shout;
}

/// <summary>
/// Makes the boss wind up and then dash in a straight line towards the target,
/// hurting and knocking back everything it plows through.
/// </summary>
public sealed partial class MegafaunaChargeActionEvent : WorldTargetActionEvent
{
    /// <summary>
    /// Seconds the boss stands still telegraphing the charge.
    /// </summary>
    [DataField]
    public float WindUp = 0.6f;

    /// <summary>
    /// Speed of the dash in tiles per second.
    /// </summary>
    [DataField]
    public float ChargeSpeed = 14f;

    /// <summary>
    /// How far past the target position the boss keeps going.
    /// </summary>
    [DataField]
    public float Overshoot = 2f;

    [DataField]
    public float MaxDistance = 14f;

    /// <summary>
    /// Number of consecutive charges. Each one re-aims at the target.
    /// </summary>
    [DataField]
    public int Charges = 1;

    /// <summary>
    /// Wind-up between consecutive charges.
    /// </summary>
    [DataField]
    public float ChainWindUp = 0.35f;

    [DataField]
    public DamageSpecifier? Damage;

    [DataField]
    public float HitRadius = 1.2f;

    [DataField]
    public float KnockbackDistance = 3f;

    [DataField]
    public float KnockbackSpeed = 10f;

    [DataField]
    public float KnockdownTime;

    /// <summary>
    /// If false, other lavaland fauna are not hurt by the charge.
    /// </summary>
    [DataField]
    public bool HitFauna;

    /// <summary>
    /// Spawned on the boss when the wind-up starts.
    /// </summary>
    [DataField]
    public EntProtoId? WindUpSpawn;

    /// <summary>
    /// Spawned on every tile the boss passes during the dash.
    /// </summary>
    [DataField]
    public EntProtoId? TrailSpawn;

    [DataField]
    public SoundSpecifier? WindUpSound;

    [DataField]
    public SoundSpecifier? HitSound;

    [DataField]
    public LocId? Shout;
}

/// <summary>
/// The boss takes off, becomes untouchable while a shadow hunts the target down,
/// then slams down on the shadow's position.
/// </summary>
public sealed partial class MegafaunaSwoopActionEvent : WorldTargetActionEvent
{
    /// <summary>
    /// Seconds the shadow follows the target.
    /// </summary>
    [DataField]
    public float TrackTime = 1.6f;

    /// <summary>
    /// Seconds between the shadow locking in place and the landing.
    /// </summary>
    [DataField]
    public float LockTime = 0.6f;

    /// <summary>
    /// How fast the shadow moves towards the target, in tiles per second.
    /// </summary>
    [DataField]
    public float ShadowSpeed = 9f;

    /// <summary>
    /// Entity that marks where the boss will land.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Shadow;

    /// <summary>
    /// Spawned at the landing spot, usually a shape spawner.
    /// </summary>
    [DataField]
    public EntProtoId? LandSpawn;

    [DataField]
    public DamageSpecifier? LandDamage;

    [DataField]
    public float LandRadius = 1.5f;

    [DataField]
    public float KnockbackDistance = 4f;

    [DataField]
    public float KnockdownTime = 1f;

    [DataField]
    public SoundSpecifier? TakeoffSound;

    [DataField]
    public SoundSpecifier? LandSound;

    [DataField]
    public LocId? Shout;
}

/// <summary>
/// Makes the boss say a random line out of a list.
/// </summary>
public sealed partial class MegafaunaShoutActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public List<LocId> Lines = new();
}

[Serializable, NetSerializable]
public enum MegafaunaVisuals : byte
{
    /// <summary>
    /// Bool, true while the boss is in the air during a swoop.
    /// </summary>
    Airborne,

    /// <summary>
    /// Bool, true while the boss winds up or performs a charge.
    /// </summary>
    Charging,
}
