using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - Drives <see cref="VehicleHandlingComponent"/> vehicles. Runs right after the mover, which
/// still reads the driver's input into <see cref="InputMoverComponent.WishDir"/>; this then replaces the
/// mover's velocity with car-style heading + speed, and turns hard speed loss and people in the way into
/// crashes and run-overs. Steering and speed are predicted; damage, throws and popups are server-side.
/// </summary>
public sealed partial class VehicleHandlingController : VirtualController
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private VehicleSystem _vehicle = default!;
    [Dependency] private VehicleControlsSystem _controls = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    public const string BumperFixture = "vehicle-bumper";

    // People (MobLayer) and other vehicles (TableLayer hulls).
    private const int BumperMask = (int) (CollisionGroup.MobLayer | CollisionGroup.MidImpassable);

    private static readonly TimeSpan CrashCooldown = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan HitCooldown = TimeSpan.FromSeconds(1);

    private readonly List<(EntityUid Vehicle, float Loss, EntityUid? Obstacle)> _crashes = new();
    private readonly List<(EntityUid Vehicle, EntityUid Victim, float Speed)> _runOvers = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedMoverController));

        base.Initialize();

        SubscribeLocalEvent<VehicleHandlingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleHandlingComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<VehicleHandlingComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<VehicleHandlingComponent, VehicleReverseActionEvent>(OnReverseAction);
    }

    private void OnMapInit(Entity<VehicleHandlingComponent> ent, ref MapInitEvent args)
    {
        // We steer the hull ourselves; stop the mover snapping it to the input direction.
        EnsureComp<NoRotateOnMoveComponent>(ent);

        if (!HasComp<PhysicsComponent>(ent) || !HasComp<FixturesComponent>(ent))
            return;

        // Movers can't hard-collide with each other, so people and other vehicles are found with a sensor.
        IPhysShape shape = new PhysShapeCircle(ent.Comp.BumperRadius);
        if (ent.Comp.BumperBounds is { } bounds)
        {
            var box = new PolygonShape();
            box.SetAsBox(bounds);
            shape = box;
        }

        _fixtures.TryCreateFixture(ent.Owner,
            shape,
            BumperFixture,
            density: 1f,
            hard: false,
            collisionLayer: 0,
            collisionMask: BumperMask);
    }

    private void OnOperatorSet(Entity<VehicleHandlingComponent> ent, ref VehicleOperatorSetEvent args)
    {
        // A new driver starts from however the vehicle is actually moving; no driver, no engine.
        ent.Comp.Velocity = args.NewOperator != null && TryComp<PhysicsComponent>(ent, out var physics)
            ? physics.LinearVelocity
            : Vector2.Zero;
        if (args.NewOperator == null)
            ent.Comp.Speed = 0f;

        // Every driver starts out in drive, with the gear lever to hand.
        ent.Comp.Reversing = false;
        if (!_timing.ApplyingState)
        {
            _actions.RemoveAction(ent.Comp.ReverseActionEntity);
            if (args.NewOperator is { } driver && ent.Comp.ReverseAction is { } reverse)
                _actions.AddAction(driver, ref ent.Comp.ReverseActionEntity, reverse, ent.Owner);
        }

        Dirty(ent);
    }

    private void OnReverseAction(Entity<VehicleHandlingComponent> ent, ref VehicleReverseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Reversing = !ent.Comp.Reversing;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.ReverseActionEntity, ent.Comp.Reversing);

        var msg = ent.Comp.Reversing ? "vehicle-gear-reverse" : "vehicle-gear-drive";
        _popup.PopupClient(Loc.GetString(msg, ("vehicle", ent.Owner)), ent.Owner, args.Performer);
    }

    #region Handling

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<VehicleHandlingComponent, InputMoverComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var handling, out var mover, out var physics, out var xform))
        {
            handling.PreSolveSpeed = 0f;

            if (prediction && !physics.Predict)
                continue;

            if (_vehicle.GetOperatorOrNull(uid) is not { } driver || !mover.CanMove)
                continue;

            if (_net.IsServer)
                SyncWalkByDefault((uid, mover), driver);

            // Weightless there's nothing for the wheels to grip: the stock mover drifts it about, but it still
            // swings round to face where it's steered. Off-grid with map gravity (the open snow around stations
            // on Glacier, TalonStride and the like) still counts as ground.
            if (_gravity.IsWeightless(uid))
            {
                if (handling.Speed != 0f)
                {
                    handling.Speed = 0f;
                    handling.Velocity = Vector2.Zero;
                    Dirty(uid, handling);
                }

                if (mover.WishDir.LengthSquared() > 0.0001f)
                {
                    var current = TransformSystem.GetWorldRotation(xform);
                    var diff = (float) Angle.ShortestDistance(current, mover.WishDir.ToWorldAngle()).Degrees;
                    var step = handling.TurnRate * frameTime;
                    if (MathF.Abs(diff) > 0.01f)
                        TransformSystem.SetWorldRotation(uid, current + Angle.FromDegrees(Math.Clamp(diff, -step, step)));
                }
                continue;
            }

            var mods = CompOrNull<MovementSpeedModifierComponent>(uid);
            var walk = mods?.CurrentWalkSpeed ?? MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
            var sprint = mods?.CurrentSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
            var topMultiplier = handling.TopSpeedMultiplier;
            if (_emag.CheckFlag(uid, EmagType.Interaction) && TryComp<VehicleControlsComponent>(uid, out var controls))
                topMultiplier += controls.EmagTopSpeedBonus; // speed limiter's gone
            var top = mover.Sprinting ? sprint * topMultiplier : walk;

            var oldHeading = TransformSystem.GetWorldRotation(xform);
            var heading = oldHeading;
            var speed = handling.Speed;
            var wish = mover.WishDir;

            if (wish.LengthSquared() > 0.0001f)
            {
                if (handling.Reversing)
                    Reverse(handling, wish, walk, frameTime, ref heading, ref speed);
                else
                    Drive(handling, wish, top, sprint, frameTime, ref heading, ref speed);
            }
            else
            {
                // Let go: roll to a stop, whichever way it was going.
                speed = speed > 0f
                    ? MathF.Max(0f, speed - handling.Coasting * frameTime)
                    : MathF.Min(0f, speed + handling.Coasting * frameTime);
            }

            // Grip: the actual velocity swings round toward where the vehicle points. Low grip slides wide.
            var desired = heading.ToWorldVec() * speed;
            var velocity = handling.Velocity;
            velocity += (desired - velocity) * MathF.Min(1f, handling.Grip * frameTime);
            if (velocity.LengthSquared() < 0.0004f && speed == 0f)
                velocity = Vector2.Zero;

            if (!MathHelper.CloseTo(speed, handling.Speed) || !velocity.EqualsApprox(handling.Velocity))
            {
                handling.Speed = speed;
                handling.Velocity = velocity;
                Dirty(uid, handling);
            }

            handling.PreSolveSpeed = velocity.Length();

            if (!heading.EqualsApprox(oldHeading))
                TransformSystem.SetWorldRotation(uid, heading);

            PhysicsSystem.SetLinearVelocity(uid, velocity, body: physics);
            PhysicsSystem.SetAngularVelocity(uid, 0f, body: physics);

            if (handling.PreSolveSpeed > 0f)
                PhysicsSystem.WakeBody(uid, body: physics);
        }
    }

    /// <summary>
    /// In drive: steer toward the wanted direction and throttle up. Pressing against the heading brakes,
    /// then pivots the vehicle round once it's nearly stopped.
    /// </summary>
    private static void Drive(VehicleHandlingComponent handling, Vector2 wish, float top, float sprint, float frameTime, ref Angle heading, ref float speed)
    {
        // Just put it in drive while still rolling backwards: stop first.
        if (speed < 0f)
        {
            speed = MathF.Min(0f, speed + handling.Braking * frameTime);
            return;
        }

        var diff = (float) Angle.ShortestDistance(heading, wish.ToWorldAngle()).Degrees;
        var absDiff = MathF.Abs(diff);

        if (absDiff > handling.ReverseAngle)
        {
            speed = MathF.Max(0f, speed - handling.Braking * frameTime);
            if (speed <= handling.PivotSpeed)
            {
                var pivot = handling.TurnRate * frameTime;
                heading += Angle.FromDegrees(Math.Clamp(diff, -pivot, pivot));
            }
            return;
        }

        // Steer: turning slows down as speed builds, and every degree turned scrubs speed.
        var fraction = top > 0f ? Math.Clamp(speed / top, 0f, 1f) : 0f;
        var turnRate = handling.TurnRate + (handling.TurnRateAtTopSpeed - handling.TurnRate) * fraction;
        var maxTurn = turnRate * frameTime;
        var turn = Math.Clamp(diff, -maxTurn, maxTurn);
        heading += Angle.FromDegrees(turn);
        speed -= speed * handling.CornerDrag * MathF.Abs(turn) * MathF.PI / 180f * fraction;

        if (absDiff > handling.ThrottleAngle)
            return;

        if (speed < top)
        {
            var accel = speed < sprint ? handling.Acceleration : handling.SpeedingAcceleration;
            speed = MathF.Min(top, speed + accel * frameTime);
        }
        else
        {
            // Dropped to walk, or slowed by something: ease down to the new cap.
            speed = MathF.Max(top, speed - handling.Braking * frameTime);
        }
    }

    /// <summary>
    /// In reverse: the vehicle keeps its nose where it is and backs its tail toward the wanted direction,
    /// slowly. Pressing toward the front brakes.
    /// </summary>
    private static void Reverse(VehicleHandlingComponent handling, Vector2 wish, float walk, float frameTime, ref Angle heading, ref float speed)
    {
        // Still rolling forwards: stop first.
        if (speed > 0f)
        {
            speed = MathF.Max(0f, speed - handling.Braking * frameTime);
            return;
        }

        var diff = (float) Angle.ShortestDistance(heading + Angle.FromDegrees(180), wish.ToWorldAngle()).Degrees;
        var absDiff = MathF.Abs(diff);

        if (absDiff > handling.ReverseAngle)
        {
            speed = MathF.Min(0f, speed + handling.Braking * frameTime);
            return;
        }

        var reverseTop = walk * handling.ReverseSpeedMultiplier;
        var maxTurn = handling.TurnRate * 0.6f * frameTime;
        heading += Angle.FromDegrees(Math.Clamp(diff, -maxTurn, maxTurn));

        if (absDiff <= handling.ThrottleAngle)
            speed = MathF.Max(-reverseTop, speed - handling.ReverseAcceleration * frameTime);
    }

    /// <summary>
    /// The walk/run key means the same behind the wheel as on foot: someone who walks by default holds it
    /// to put their foot down, everyone else holds it to crawl along.
    /// </summary>
    private void SyncWalkByDefault(Entity<InputMoverComponent> vehicle, EntityUid driver)
    {
        if (!TryComp<InputMoverComponent>(driver, out var driverMover)
            || driverMover.WalkByDefault == vehicle.Comp.WalkByDefault)
            return;

        vehicle.Comp.WalkByDefault = driverMover.WalkByDefault;
        Dirty(vehicle);
    }

    /// <summary>
    /// Whatever the solver took off our forward speed is real: keep it, and if it was a lot, that's a crash.
    /// </summary>
    public override void UpdateAfterSolve(bool prediction, float frameTime)
    {
        base.UpdateAfterSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<VehicleHandlingComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var handling, out var physics, out var xform))
        {
            var obstacle = handling.LastObstacle;
            handling.LastObstacle = null;

            if (handling.PreSolveSpeed <= 0f || (prediction && !physics.Predict))
                continue;

            // The solver only changes our velocity when we hit something.
            var actualVelocity = physics.LinearVelocity;
            var loss = handling.PreSolveSpeed - actualVelocity.Length();
            if (loss <= 0.05f)
                continue;

            var heading = TransformSystem.GetWorldRotation(xform).ToWorldVec();
            handling.Velocity = actualVelocity;
            var forward = Vector2.Dot(actualVelocity, heading);
            handling.Speed = handling.Speed >= 0f
                ? MathF.Min(handling.Speed, MathF.Max(0f, forward))
                : MathF.Max(handling.Speed, MathF.Min(0f, forward));
            Dirty(uid, handling);

            if (prediction || !_net.IsServer || loss < handling.CrashMinSpeedLoss || _timing.CurTime < handling.NextCrash)
                continue;

            handling.NextCrash = _timing.CurTime + CrashCooldown;
            _crashes.Add((uid, loss, obstacle));
        }
    }

    #endregion

    #region Collisions

    private void OnStartCollide(Entity<VehicleHandlingComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != BumperFixture)
        {
            if (args.OurFixture.Hard && args.OtherFixture.Hard)
                ent.Comp.LastObstacle = args.OtherEntity;
            return;
        }

        var other = args.OtherEntity;
        if (other == ent.Owner
            || MathF.Abs(ent.Comp.Speed) < ent.Comp.RunOverMinSpeed
            || !_vehicle.HasOperator(ent.Owner)
            || _controls.IsAboard(ent, other))
            return;

        var now = _timing.CurTime;
        if (ent.Comp.RecentHits.TryGetValue(other, out var until) && now < until)
            return;

        if (HasComp<MobStateComponent>(other))
        {
            PruneHits(ent.Comp, now);
            ent.Comp.RecentHits[other] = now + HitCooldown;

            var speed = MathF.Abs(ent.Comp.Speed);
            ent.Comp.Speed *= 1f - ent.Comp.RunOverSpeedLoss;
            ent.Comp.Velocity *= 1f - ent.Comp.RunOverSpeedLoss;
            Dirty(ent);

            if (_net.IsServer)
                _runOvers.Add((ent.Owner, other, speed));
        }
        else if (HasComp<VehicleComponent>(other) && args.OtherFixture.Hard)
        {
            // Vehicles don't physically collide with each other (both are movers), so a head-on is a crash.
            PruneHits(ent.Comp, now);
            ent.Comp.RecentHits[other] = now + HitCooldown;

            var loss = MathF.Abs(ent.Comp.Speed);
            ent.Comp.Speed = 0f;
            ent.Comp.Velocity = Vector2.Zero;
            Dirty(ent);

            if (_net.IsServer && loss >= ent.Comp.CrashMinSpeedLoss && now >= ent.Comp.NextCrash)
            {
                ent.Comp.NextCrash = now + CrashCooldown;
                _crashes.Add((ent.Owner, loss, other));
            }
        }
    }

    private static void PruneHits(VehicleHandlingComponent comp, TimeSpan now)
    {
        if (comp.RecentHits.Count < 16)
            return;

        var expired = new List<EntityUid>();
        foreach (var (hit, until) in comp.RecentHits)
        {
            if (until < now)
                expired.Add(hit);
        }

        foreach (var hit in expired)
            comp.RecentHits.Remove(hit);
    }

    #endregion

    #region Consequences (server)

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (vehicle, loss, obstacle) in _crashes)
            DoCrash(vehicle, loss, obstacle);
        _crashes.Clear();

        foreach (var (vehicle, victim, speed) in _runOvers)
            DoRunOver(vehicle, victim, speed);
        _runOvers.Clear();
    }

    private void DoCrash(EntityUid vehicle, float loss, EntityUid? obstacle)
    {
        if (TerminatingOrDeleted(vehicle) || !TryComp<VehicleHandlingComponent>(vehicle, out var handling))
            return;

        var driver = _vehicle.GetOperatorOrNull(vehicle);
        var scale = loss * loss / MathF.Max(0.1f, handling.CrashMinSpeedLoss);

        _audio.PlayPvs(handling.CrashSound, vehicle);
        _popup.PopupEntity(Loc.GetString("vehicle-crash", ("vehicle", vehicle)), vehicle, PopupType.MediumCaution);

        // The driver's share arrives through the vehicle's own damage transfer.
        _damageable.TryChangeDamage(vehicle, handling.CrashDamage * scale, origin: driver);

        if (obstacle is { } hit && !TerminatingOrDeleted(hit) && hit != vehicle)
            _damageable.TryChangeDamage(hit, handling.ObstacleDamage * scale, origin: driver);

        var riders = new List<EntityUid>();
        if (_vehicle.TryGetOperatorContainer(vehicle, out var container))
            riders.AddRange(container.ContainedEntities);

        var strap = CompOrNull<StrapComponent>(vehicle);
        if (strap != null)
            riders.AddRange(strap.BuckledEntities);

        var heading = TransformSystem.GetWorldRotation(vehicle).ToWorldVec();
        var eject = strap != null && handling.EjectSpeedLoss is { } ejectLoss && loss >= ejectLoss;

        foreach (var rider in riders)
        {
            if (rider != driver)
                _damageable.TryChangeDamage(rider, handling.OccupantDamage * scale, origin: driver);

            if (!eject)
                continue;

            // Open vehicles fling their riders over the front.
            _buckle.Unbuckle(rider, null);
            _stun.TryKnockdown(rider, TimeSpan.FromSeconds(2));
            _throwing.TryThrow(rider, heading * (1f + loss * 0.3f), baseThrowSpeed: 4f + loss, recoil: false);
            _popup.PopupEntity(Loc.GetString("vehicle-crash-thrown", ("vehicle", vehicle)), rider, rider, PopupType.LargeCaution);
        }

        _adminLog.Add(LogType.Damaged, LogImpact.Low,
            $"{ToPrettyString(vehicle)} driven by {ToPrettyString(driver)} crashed into {ToPrettyString(obstacle)}, losing {loss:F1} tiles/s");
    }

    private void DoRunOver(EntityUid vehicle, EntityUid victim, float speed)
    {
        if (TerminatingOrDeleted(vehicle) || TerminatingOrDeleted(victim)
            || !TryComp<VehicleHandlingComponent>(vehicle, out var handling))
            return;

        var driver = _vehicle.GetOperatorOrNull(vehicle);

        // Emagged: safeties off. Emagged quads hit harder and threw people ahead of them.
        var emagged = _emag.CheckFlag(vehicle, EmagType.Interaction);
        var multiplier = emagged && TryComp<VehicleControlsComponent>(vehicle, out var controls)
            ? controls.EmagRunOverMultiplier
            : 1f;

        _damageable.TryChangeDamage(victim, handling.RunOverDamage * (speed * multiplier), origin: driver);
        _stun.TryKnockdown(victim, TimeSpan.FromSeconds(handling.RunOverKnockdown * multiplier));

        // Knock them aside, away from the centreline and a little ahead, clear of the vehicle's path
        // (behind it, when reversing over someone).
        var heading = TransformSystem.GetWorldRotation(vehicle).ToWorldVec();
        if (handling.Speed < 0f)
            heading = -heading;
        var side = new Vector2(-heading.Y, heading.X);
        var offset = TransformSystem.GetWorldPosition(victim) - TransformSystem.GetWorldPosition(vehicle);
        var dot = Vector2.Dot(offset, side);
        if (MathF.Abs(dot) < 0.05f)
            dot = _random.Prob(0.5f) ? 1f : -1f;
        var direction = emagged
            ? (heading + side * MathF.Sign(dot) * 0.2f).Normalized() * (2f + speed * 0.3f)
            : (side * MathF.Sign(dot) + heading * 0.5f).Normalized() * (1f + speed * 0.2f);
        _throwing.TryThrow(victim, direction, baseThrowSpeed: 4f + speed, recoil: false);

        // Quads take a knock from every person they hit.
        _damageable.TryChangeDamage(vehicle, handling.CrashDamage * 0.5f, origin: driver);

        _audio.PlayPvs(handling.RunOverSound, victim);
        _popup.PopupEntity(Loc.GetString("vehicle-run-over-victim", ("vehicle", vehicle)), victim, victim, PopupType.LargeCaution);
        if (driver != null)
            _popup.PopupEntity(Loc.GetString("vehicle-run-over-driver", ("victim", victim)), vehicle, driver.Value, PopupType.MediumCaution);

        _adminLog.Add(LogType.Damaged, LogImpact.Medium,
            $"{ToPrettyString(driver):player} ran over {ToPrettyString(victim):target} with {ToPrettyString(vehicle)} at {speed:F1} tiles/s");
    }

    #endregion
}
