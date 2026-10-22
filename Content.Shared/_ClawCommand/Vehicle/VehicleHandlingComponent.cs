using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - Car-style handling, crashes and run-overs for a driven vehicle, layered on top of the
/// upstream mover (see <see cref="VehicleHandlingController"/>).
/// <para>
/// The vehicle keeps a heading and a speed. Holding a direction steers toward it (more slowly the faster
/// you go) and throttles up: quickly to sprint speed, then slowly on toward the top speed while you keep
/// going straight ("speeding"). Corners scrub speed, pressing against the heading brakes, and letting go coasts.
/// Hitting something hard enough to lose a lot of speed is a crash; driving through someone at speed runs them over.
/// </para>
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleHandlingComponent : Component
{
    #region Driving

    /// <summary>Tiles/s² while below the vehicle's sprint speed.</summary>
    [DataField]
    public float Acceleration = 4f;

    /// <summary>Tiles/s² between sprint speed and top speed; how long it takes to build up to "speeding".</summary>
    [DataField]
    public float SpeedingAcceleration = 0.9f;

    /// <summary>Top speed as a multiple of the vehicle's sprint speed. Walking (Shift) caps at walk speed instead.</summary>
    [DataField]
    public float TopSpeedMultiplier = 1.5f;

    /// <summary>Tiles/s² when pressing against the heading.</summary>
    [DataField]
    public float Braking = 9f;

    /// <summary>Tiles/s² lost while no direction is held.</summary>
    [DataField]
    public float Coasting = 3f;

    /// <summary>Degrees/s the vehicle can turn when (nearly) stopped.</summary>
    [DataField]
    public float TurnRate = 260f;

    /// <summary>Degrees/s the vehicle can turn at top speed.</summary>
    [DataField]
    public float TurnRateAtTopSpeed = 100f;

    /// <summary>Fraction of speed lost per radian turned, at top speed (scaled down with speed).</summary>
    [DataField]
    public float CornerDrag = 0.35f;

    /// <summary>Only throttles while the wanted direction is within this many degrees of the heading.</summary>
    [DataField]
    public float ThrottleAngle = 60f;

    /// <summary>Pressing further than this many degrees from the heading brakes instead of steering.</summary>
    [DataField]
    public float ReverseAngle = 120f;

    /// <summary>Below this speed, pressing against the heading pivots the vehicle round instead of braking.</summary>
    [DataField]
    public float PivotSpeed = 1f;

    /// <summary>
    /// How hard the tyres bite: how quickly the actual velocity swings round to the heading.
    /// High (25+) drives on rails; low (4-6) slides wide through corners and drifts.
    /// </summary>
    [DataField]
    public float Grip = 12f;

    /// <summary>In reverse gear: directions back the vehicle toward them instead of driving there.</summary>
    [DataField, AutoNetworkedField]
    public bool Reversing;

    /// <summary>Top speed in reverse, as a multiple of the vehicle's walk speed.</summary>
    [DataField]
    public float ReverseSpeedMultiplier = 0.6f;

    /// <summary>Tiles/s² when backing up.</summary>
    [DataField]
    public float ReverseAcceleration = 2.5f;

    /// <summary>Gear lever granted to the driver. Null for no reverse.</summary>
    [DataField]
    public EntProtoId? ReverseAction = "ActionVehicleReverse";

    [DataField, AutoNetworkedField]
    public EntityUid? ReverseActionEntity;

    /// <summary>
    /// Current engine speed in tiles/s along the heading; negative while reversing.
    /// Networked so client prediction re-simulates it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Speed;

    /// <summary>Actual velocity, which lags behind heading * speed by <see cref="Grip"/>.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 Velocity;

    #endregion

    #region Engine

    /// <summary>
    /// Loops while someone's at the wheel and it can run. Copies at each of <see cref="EnginePitches"/> are
    /// crossfaded by speed, so it revs up as it goes. Null for pedal power.
    /// </summary>
    [DataField]
    public SoundSpecifier? EngineSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehicleengineidle.ogg");

    [DataField]
    public float EngineVolume = -6f;

    /// <summary>Pitch of each engine layer, from idle up to flat out.</summary>
    [DataField]
    public List<float> EnginePitches = new() { 0.8f, 1.15f, 1.6f };

    #endregion

    #region Crashes

    /// <summary>Losing at least this much speed (tiles/s) in one physics step is a crash.</summary>
    [DataField]
    public float CrashMinSpeedLoss = 3f;

    /// <summary>
    /// Damage to the vehicle per tile/s of speed lost, scaled up again by (loss / CrashMinSpeedLoss) so fast
    /// crashes hurt a lot more than bumps. The driver gets their share through the vehicle's damage transfer.
    /// </summary>
    [DataField]
    public DamageSpecifier CrashDamage = new();

    /// <summary>Damage to whatever was hit, same scaling.</summary>
    [DataField]
    public DamageSpecifier ObstacleDamage = new();

    /// <summary>Damage to passengers and open-vehicle riders other than the driver, same scaling.</summary>
    [DataField]
    public DamageSpecifier OccupantDamage = new();

    /// <summary>Open vehicles throw their riders off in crashes that lose at least this much speed. Null never ejects.</summary>
    [DataField]
    public float? EjectSpeedLoss = 5f;

    [DataField]
    public SoundSpecifier? CrashSound = new SoundCollectionSpecifier("MetalSlam");

    #endregion

    #region Running people over

    /// <summary>Hitting a mob at or above this speed runs them over.</summary>
    [DataField]
    public float RunOverMinSpeed = 2.5f;

    /// <summary>Damage to the victim per tile/s of speed.</summary>
    [DataField]
    public DamageSpecifier RunOverDamage = new();

    [DataField]
    public float RunOverKnockdown = 2f;

    /// <summary>Fraction of speed the vehicle loses per person hit.</summary>
    [DataField]
    public float RunOverSpeedLoss = 0.25f;

    [DataField]
    public SoundSpecifier? RunOverSound = new SoundCollectionSpecifier("Punch");

    /// <summary>Radius of the sensor fixture used to find people in the way. Should reach just past the hull.</summary>
    [DataField]
    public float BumperRadius = 0.6f;

    /// <summary>Box sensor instead of a circle, for long vehicles. Should sit just outside the hull fixture.</summary>
    [DataField]
    public Box2? BumperBounds;

    #endregion

    // Transient per-step bookkeeping, not networked.
    [ViewVariables]
    public float PreSolveSpeed;

    [ViewVariables]
    public EntityUid? LastObstacle;

    [ViewVariables]
    public TimeSpan NextCrash;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentHits = new();
}
