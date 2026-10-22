using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Gravity;
using Content.Shared._ClawCommand.Vehicle;
using Content.Shared.Buckle;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - a connected player drives with real key presses, so client prediction is in the loop:
/// the vehicle must turn to face the way it is steered on both sides and pull away faster than walking,
/// with or without the walk/run key held.
/// </summary>
[TestFixture]
public sealed class VehicleClientDriveTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [TestCase("VehicleSpaceBike", false)]
    [TestCase("VehiclePickupTruck", false)]
    [TestCase("VehicleHumvee", false)]
    [TestCase("VehicleSpaceBike", true)]
    [TestCase("VehiclePickupTruck", true)]
    [TestCase("VehicleHumvee", true)]
    public async Task PlayerDrivesAndTurns(string proto, bool planet)
    {
        await DriveAndTurn(proto, planet, false);
    }

    /// <summary>
    /// Glacier, TalonStride and friends: the snow round the station is open map with map gravity and air,
    /// no grid at all. Vehicles still have to drive properly out there.
    /// </summary>
    [TestCase("VehicleSpaceBike")]
    [TestCase("VehiclePickupTruck")]
    [TestCase("VehicleMonoSupron")]
    public async Task PlayerDrivesOnOpenSnow(string proto)
    {
        await DriveAndTurn(proto, false, true);
    }

    private async Task DriveAndTurn(string proto, bool planet, bool openMap)
    {
        EntityUid vehicle = default;

        await Server.WaitPost(() =>
        {
            if (openMap)
            {
                var mapGravity = SEntMan.EnsureComponent<GravityComponent>(MapData.MapUid);
                mapGravity.Enabled = true;
                mapGravity.Inherent = true;
                SEntMan.Dirty(MapData.MapUid, mapGravity);

                var coords = new EntityCoordinates(MapData.MapUid, 70.5f, 0.5f);
                Transform.SetCoordinates(SPlayer, coords);
                vehicle = SEntMan.SpawnEntity(proto, coords);
                return;
            }

            if (planet)
            {
                // Snow planet: the map itself is the ground, with inherent map gravity and no station grid
                // (what BiomeSystem.EnsurePlanet sets up, minus the random rocks and trees).
                var mapGrid = SEntMan.EnsureComponent<MapGridComponent>(MapData.MapUid);
                var mapGravity = SEntMan.EnsureComponent<GravityComponent>(MapData.MapUid);
                mapGravity.Enabled = true;
                mapGravity.Inherent = true;
                SEntMan.Dirty(MapData.MapUid, mapGravity);
                var snow = new Tile(TileMan["FloorSnow"].TileId);
                for (var x = 40; x <= 100; x++)
                for (var y = -30; y <= 30; y++)
                    MapSystem.SetTile(MapData.MapUid, mapGrid, new Vector2i(x, y), snow);

                var coords = new EntityCoordinates(MapData.MapUid, 70.5f, 0.5f);
                Transform.SetCoordinates(SPlayer, coords);
                vehicle = SEntMan.SpawnEntity(proto, coords);
                return;
            }

            var plating = new Tile(TileMan["Plating"].TileId);
            for (var x = -30; x <= 30; x++)
            for (var y = -30; y <= 30; y++)
                MapSystem.SetTile(MapData.Grid, new Vector2i(x, y), plating);

            var gravity = SEntMan.EnsureComponent<GravityComponent>(MapData.Grid);
            SEntMan.System<GravitySystem>().EnableGravity(MapData.Grid, gravity);
            gravity.Inherent = true;

            vehicle = SEntMan.SpawnEntity(proto, SEntMan.GetCoordinates(PlayerCoords));
        });

        await RunTicks(10);

        await Server.WaitPost(() =>
        {
            if (SEntMan.HasComponent<StrapVehicleComponent>(vehicle))
                Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(SPlayer, SPlayer, vehicle), $"{proto}: buckle");
            else
                Assert.That(SEntMan.System<VehicleSystem>().TryEnter(vehicle, SPlayer), $"{proto}: enter");
        });

        await RunTicks(10);

        var start = SEntMan.System<SharedTransformSystem>().GetWorldPosition(vehicle);
        TestContext.Out.WriteLine($"{proto} planet={planet}: grid={SEntMan.GetComponent<TransformComponent>(vehicle).GridUid} map={MapData.MapUid} weightless={SEntMan.System<SharedGravitySystem>().IsWeightless(vehicle)}");

        await SetMovementKey(DirectionFlag.East, BoundKeyState.Down);
        await RunSeconds(2);
        Report(proto, "east (default)", vehicle, start);
        AssertFacing(proto, vehicle, new Vector2(1, 0));
        var defaultSpeed = SEntMan.GetComponent<PhysicsComponent>(vehicle).LinearVelocity.Length();
        await SetMovementKey(DirectionFlag.East, BoundKeyState.Up);

        await SetMovementKey(DirectionFlag.North, BoundKeyState.Down);
        await RunSeconds(2);
        Report(proto, "north", vehicle, start);
        AssertFacing(proto, vehicle, new Vector2(0, 1));
        await SetMovementKey(DirectionFlag.North, BoundKeyState.Up);

        // The walk/run key.
        await SetKey(EngineKeyFunctions.Walk, BoundKeyState.Down);
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Down);
        await RunSeconds(2.5f);
        Report(proto, "west + walk key", vehicle, start);
        AssertFacing(proto, vehicle, new Vector2(-1, 0));
        var walkKeySpeed = SEntMan.GetComponent<PhysicsComponent>(vehicle).LinearVelocity.Length();
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Up);
        await SetKey(EngineKeyFunctions.Walk, BoundKeyState.Up);

        TestContext.Out.WriteLine($"{proto}: default {defaultSpeed:F2}, walk key {walkKeySpeed:F2}");
        Assert.That(MathF.Max(defaultSpeed, walkKeySpeed), Is.GreaterThan(5f), $"{proto} never got going");
    }

    [TestCase("VehicleHumvee")]
    [TestCase("VehicleAPC")]
    [TestCase("VehicleAmbulance")]
    public async Task ClickToClimbIn(string proto)
    {
        EntityUid vehicle = default;
        await Server.WaitPost(() =>
        {
            var plating = new Tile(TileMan["Plating"].TileId);
            for (var x = -5; x <= 5; x++)
            for (var y = -5; y <= 5; y++)
                MapSystem.SetTile(MapData.Grid, new Vector2i(x, y), plating);

            vehicle = SEntMan.SpawnEntity(proto, SEntMan.GetCoordinates(TargetCoords));
        });
        await RunTicks(5);

        // Empty hand click on the hull, then wait out the climb-in do-after.
        await Interact(vehicle, SEntMan.GetCoordinates(TargetCoords));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<VehicleSystem>().GetOperatorOrNull(vehicle), Is.EqualTo(SPlayer),
                $"{proto}: clicking it should climb in and take the wheel");
        });
    }

    [Test]
    public async Task NoKeysNoGo()
    {
        EntityUid vehicle = default;
        await Server.WaitPost(() =>
        {
            var plating = new Tile(TileMan["Plating"].TileId);
            for (var x = -10; x <= 10; x++)
            for (var y = -10; y <= 10; y++)
                MapSystem.SetTile(MapData.Grid, new Vector2i(x, y), plating);

            vehicle = SEntMan.SpawnEntity("VehicleAmbulance", SEntMan.GetCoordinates(PlayerCoords));
        });
        await RunTicks(5);
        await Server.WaitPost(() => Assert.That(SEntMan.System<VehicleSystem>().TryEnter(vehicle, SPlayer)));
        await RunTicks(5);

        var start = SEntMan.System<SharedTransformSystem>().GetWorldPosition(vehicle);
        await SetMovementKey(DirectionFlag.East, BoundKeyState.Down);
        await RunSeconds(1);
        await SetMovementKey(DirectionFlag.East, BoundKeyState.Up);

        await Server.WaitAssertion(() =>
        {
            var moved = SEntMan.System<SharedTransformSystem>().GetWorldPosition(vehicle) - start;
            Assert.That(moved.Length(), Is.LessThan(0.1f), "It shouldn't go anywhere without its keys.");
            Assert.That(SEntMan.GetComponent<VehicleControlsComponent>(vehicle).NextIgnitionHint, Is.GreaterThan(TimeSpan.Zero),
                "The driver should be told it needs its keys.");
        });
    }

    /// <summary>
    /// The gear lever: in reverse, holding the key behind the vehicle backs it up nose-first, slowly.
    /// </summary>
    [Test]
    public async Task ReverseGearBacksUp()
    {
        var vehicle = await SpawnOnPlating("VehiclePickupTruck");
        await Server.WaitPost(() => Assert.That(SEntMan.System<VehicleSystem>().TryEnter(vehicle, SPlayer)));
        await RunTicks(5);

        // Drive east and stop.
        await SetMovementKey(DirectionFlag.East, BoundKeyState.Down);
        await RunSeconds(1);
        await SetMovementKey(DirectionFlag.East, BoundKeyState.Up);
        await RunSeconds(2);

        await Server.WaitPost(() =>
        {
            var handling = SEntMan.GetComponent<VehicleHandlingComponent>(vehicle);
            Assert.That(handling.ReverseActionEntity, Is.Not.Null, "The driver should get the gear lever.");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(handling.ReverseActionEntity!.Value).EntityPrototype?.ID,
                Is.EqualTo("ActionVehicleReverse"));
            SEntMan.EventBus.RaiseLocalEvent(vehicle, new VehicleReverseActionEvent { Performer = SPlayer });
            Assert.That(handling.Reversing, "The lever should shift it into reverse.");
        });

        var start = SEntMan.System<SharedTransformSystem>().GetWorldPosition(vehicle);
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Down);
        await RunSeconds(1.5f);
        Report("VehiclePickupTruck", "reversing west", vehicle, start);
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Up);

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.System<SharedTransformSystem>();
            var moved = xform.GetWorldPosition(vehicle) - start;
            var facing = xform.GetWorldRotation(vehicle).ToWorldVec();
            var velocity = SEntMan.GetComponent<PhysicsComponent>(vehicle).LinearVelocity;
            Assert.That(moved.X, Is.LessThan(-1f), "Reversing with west held should back it up westward.");
            Assert.That(facing.X, Is.GreaterThan(0.9f), "It should still face east while backing up.");
            Assert.That(velocity.Length(), Is.LessThan(4.5f), "Reverse should be slow.");
        });

        // Back in drive, west turns it round and drives off forwards.
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(vehicle, new VehicleReverseActionEvent { Performer = SPlayer }));
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Down);
        await RunSeconds(2.5f);
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Up);
        AssertFacing("VehiclePickupTruck", vehicle, new Vector2(-1, 0));
    }

    /// <summary>
    /// The Mono-Supron is drawn side-on only: it shows its right-facing art driving right, keeps it going up,
    /// and swaps to the left-facing art driving left.
    /// </summary>
    [Test]
    public async Task MonoSupronFacesItsWay()
    {
        var vehicle = await SpawnOnPlating("VehicleMonoSupron");
        await Server.WaitPost(() => Assert.That(SEntMan.System<VehicleSystem>().TryEnter(vehicle, SPlayer)));
        await RunTicks(5);

        string State()
        {
            var side = CEntMan.GetComponent<VehicleSideViewComponent>(ToClient(vehicle));
            return side.ShownState ?? string.Empty;
        }

        await SetMovementKey(DirectionFlag.East, BoundKeyState.Down);
        await RunSeconds(1);
        await SetMovementKey(DirectionFlag.East, BoundKeyState.Up);
        Assert.That(State(), Is.EqualTo("miyahira_orange_f"), "Driving right should show the right-facing art.");

        await SetMovementKey(DirectionFlag.North, BoundKeyState.Down);
        await RunSeconds(1);
        await SetMovementKey(DirectionFlag.North, BoundKeyState.Up);
        Assert.That(State(), Is.EqualTo("miyahira_orange_f"), "Driving up should keep the last side.");

        await SetMovementKey(DirectionFlag.West, BoundKeyState.Down);
        await RunSeconds(1.5f);
        await SetMovementKey(DirectionFlag.West, BoundKeyState.Up);
        Assert.That(State(), Is.EqualTo("miyahira_orange"), "Driving left should show the left-facing art.");
    }

    /// <summary>
    /// Stations are often rotated on their map: the vehicle has to travel the way its nose points, not skew off.
    /// </summary>
    [Test]
    public async Task RotatedStationDrivesStraight()
    {
        await Server.WaitPost(() => Transform.SetLocalRotation(MapData.Grid, Angle.FromDegrees(58)));
        var vehicle = await SpawnOnPlating("VehiclePickupTruck");
        await Server.WaitPost(() => Assert.That(SEntMan.System<VehicleSystem>().TryEnter(vehicle, SPlayer)));
        await RunTicks(5);

        var start = SEntMan.System<SharedTransformSystem>().GetWorldPosition(vehicle);
        await SetMovementKey(DirectionFlag.East, BoundKeyState.Down);
        await RunSeconds(2);
        Report("VehiclePickupTruck", "rotated station", vehicle, start);

        await Server.WaitAssertion(() =>
        {
            var facing = SEntMan.System<SharedTransformSystem>().GetWorldRotation(vehicle).ToWorldVec();
            var velocity = SEntMan.GetComponent<PhysicsComponent>(vehicle).LinearVelocity;
            Assert.That(velocity.Length(), Is.GreaterThan(3f), "It should get going.");
            Assert.That(Vector2.Dot(facing, Vector2.Normalize(velocity)), Is.GreaterThan(0.95f),
                $"It should travel where it points: facing {facing}, moving {velocity}");
        });
        await SetMovementKey(DirectionFlag.East, BoundKeyState.Up);
    }

    private async Task<EntityUid> SpawnOnPlating(string proto)
    {
        EntityUid vehicle = default;
        await Server.WaitPost(() =>
        {
            var plating = new Tile(TileMan["Plating"].TileId);
            for (var x = -20; x <= 20; x++)
            for (var y = -20; y <= 20; y++)
                MapSystem.SetTile(MapData.Grid, new Vector2i(x, y), plating);

            var gravity = SEntMan.EnsureComponent<GravityComponent>(MapData.Grid);
            SEntMan.System<GravitySystem>().EnableGravity(MapData.Grid, gravity);
            gravity.Inherent = true;

            vehicle = SEntMan.SpawnEntity(proto, SEntMan.GetCoordinates(PlayerCoords));
        });
        await RunTicks(5);
        return vehicle;
    }

    private void Report(string proto, string leg, EntityUid vehicle, Vector2 start)
    {
        var sXform = SEntMan.System<SharedTransformSystem>();
        var cXform = CEntMan.System<SharedTransformSystem>();
        var cVehicle = ToClient(vehicle);
        var handling = SEntMan.GetComponent<VehicleHandlingComponent>(vehicle);
        TestContext.Out.WriteLine(
            $"{proto} {leg}: pos={sXform.GetWorldPosition(vehicle) - start} " +
            $"sFacing={sXform.GetWorldRotation(vehicle).ToWorldVec()} cFacing={cXform.GetWorldRotation(cVehicle).ToWorldVec()} " +
            $"sVel={SEntMan.GetComponent<PhysicsComponent>(vehicle).LinearVelocity} cVel={CEntMan.GetComponent<PhysicsComponent>(cVehicle).LinearVelocity} " +
            $"engine={handling.Speed:F2} cEngine={CEntMan.GetComponent<VehicleHandlingComponent>(cVehicle).Speed:F2}");
    }

    private void AssertFacing(string proto, EntityUid vehicle, Vector2 dir)
    {
        var sFacing = SEntMan.System<SharedTransformSystem>().GetWorldRotation(vehicle).ToWorldVec();
        var cFacing = CEntMan.System<SharedTransformSystem>().GetWorldRotation(ToClient(vehicle)).ToWorldVec();
        Assert.That(Vector2.Dot(sFacing, dir), Is.GreaterThan(0.9f), $"{proto}: server facing {sFacing}, wanted {dir}");
        Assert.That(Vector2.Dot(cFacing, dir), Is.GreaterThan(0.9f), $"{proto}: client facing {cFacing}, wanted {dir}");
    }
}
