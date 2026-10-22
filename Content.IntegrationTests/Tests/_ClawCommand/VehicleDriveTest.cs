using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Gravity;
using Content.Shared._ClawCommand.Vehicle;
using Content.Shared.Buckle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Tag;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - every drivable vehicle can be boarded the normal way, picks up its driver,
/// drives off when its key is in, turns to face where it is steered and goes faster than walking.
/// </summary>
[TestFixture]
public sealed class VehicleDriveTest : GameTest
{
    [Test]
    public async Task EveryVehicleBoardsAndDrives()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        var vehicles = protoMan.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.TryGetComponent<VehicleHandlingComponent>(out _, factory))
            .Select(p => p.ID)
            .OrderBy(id => id)
            .ToList();
        Assert.That(vehicles, Is.Not.Empty);

        await server.WaitPost(() =>
        {
            var map = entMan.System<SharedMapSystem>();
            var plating = new Tile(tileDefs["Plating"].TileId);
            for (var x = -40; x <= 40; x++)
            for (var y = -40; y <= 40; y++)
                map.SetTile(testMap.Grid, new Vector2i(x, y), plating);

            var gravity = entMan.EnsureComponent<GravityComponent>(testMap.Grid);
            entMan.System<GravitySystem>().EnableGravity(testMap.Grid, gravity);
            gravity.Inherent = true;
        });

        var failures = new List<string>();
        var enterText = string.Empty;
        await server.WaitPost(() => enterText = Loc.GetString("container-vehicle-verb-enter"));

        foreach (var proto in vehicles)
        {
            EntityUid vehicle = default;
            EntityUid driver = default;
            var start = Vector2.Zero;

            await server.WaitPost(() =>
            {
                var xformSys = entMan.System<SharedTransformSystem>();
                vehicle = entMan.SpawnEntity(proto, new EntityCoordinates(testMap.Grid, 0.5f, 0.5f));
                xformSys.SetLocalRotation(vehicle, Angle.Zero);

                // Stand beside the hull, the way a player walks up to it.
                var reach = 0.5f;
                if (entMan.TryGetComponent<FixturesComponent>(vehicle, out var fixtures))
                {
                    foreach (var fix in fixtures.Fixtures.Values)
                    {
                        if (!fix.Hard)
                            continue;
                        var box = fix.Shape.ComputeAABB(new Robust.Shared.Physics.Transform(Vector2.Zero, 0f), 0);
                        reach = MathF.Max(reach, box.Right);
                    }
                }

                driver = entMan.SpawnEntity("MobHuman", new EntityCoordinates(testMap.Grid, 0.5f + reach + 0.45f, 0.5f));
                start = xformSys.GetWorldPosition(vehicle);
            });

            await pair.RunTicksSync(5);

            var boarded = false;
            await server.WaitPost(() =>
            {
                var vehicleSys = entMan.System<VehicleSystem>();
                var verbs = entMan.System<SharedVerbSystem>();

                // Keys in the ignition.
                if (entMan.TryGetComponent<GenericKeyedVehicleComponent>(vehicle, out var keyed))
                {
                    var tags = keyed.KeyWhitelist.Tags ?? new();
                    var keyProto = protoMan.EnumeratePrototypes<EntityPrototype>()
                        .FirstOrDefault(p => !p.Abstract
                                             && p.TryGetComponent<TagComponent>(out var tag, factory)
                                             && tag.Tags.Any(t => tags.Contains(t)));
                    if (keyProto == null)
                    {
                        failures.Add($"{proto}: no key prototype for {string.Join(",", tags)}");
                    }
                    else
                    {
                        var key = entMan.SpawnEntity(keyProto.ID, entMan.GetComponent<TransformComponent>(vehicle).Coordinates);
                        if (!entMan.System<ItemSlotsSystem>().TryInsert(vehicle, keyed.ContainerId, key, null))
                            failures.Add($"{proto}: key {keyProto.ID} would not go in");
                    }
                }

                if (entMan.HasComponent<StrapVehicleComponent>(vehicle))
                {
                    boarded = entMan.System<SharedBuckleSystem>().TryBuckle(driver, driver, vehicle);
                    if (!boarded)
                        failures.Add($"{proto}: could not buckle onto it");
                }
                else if (entMan.HasComponent<ContainerVehicleComponent>(vehicle))
                {
                    var hasVerb = verbs.GetLocalVerbs(vehicle, driver, typeof(AlternativeVerb))
                        .Any(v => v.Text == enterText);
                    if (!hasVerb)
                        failures.Add($"{proto}: no Enter verb from beside the hull");

                    boarded = vehicleSys.TryEnter(vehicle, driver);
                    if (!boarded)
                        failures.Add($"{proto}: TryEnter failed");
                }
                else
                {
                    failures.Add($"{proto}: neither a strap nor a container vehicle");
                }

                if (boarded && vehicleSys.GetOperatorOrNull(vehicle) != driver)
                {
                    failures.Add($"{proto}: boarded but the driver is not the operator");
                    boarded = false;
                }

                if (boarded)
                    entMan.GetComponent<InputMoverComponent>(vehicle).HeldMoveButtons = MoveButtons.Right;
            });

            if (boarded)
            {
                await pair.RunSeconds(2.5f);

                await server.WaitPost(() =>
                {
                    var xformSys = entMan.System<SharedTransformSystem>();
                    var mover = entMan.GetComponent<InputMoverComponent>(vehicle);
                    var physics = entMan.GetComponent<PhysicsComponent>(vehicle);
                    var moved = xformSys.GetWorldPosition(vehicle) - start;
                    var facing = xformSys.GetWorldRotation(vehicle).ToWorldVec();
                    var speed = physics.LinearVelocity.Length();
                    var walk = entMan.GetComponentOrNull<MovementSpeedModifierComponent>(vehicle)?.CurrentWalkSpeed ?? 2.5f;

                    var handling = entMan.GetComponent<VehicleHandlingComponent>(vehicle);
                    var weightless = entMan.System<SharedGravitySystem>().IsWeightless(vehicle);
                    TestContext.Out.WriteLine($"{proto}: canMove={mover.CanMove} moved={moved} speed={speed:F2} facing={facing} engine={handling.Speed:F2} wish={mover.WishDir} sprint={mover.Sprinting} weightless={weightless} grid={entMan.GetComponent<TransformComponent>(vehicle).GridUid}");

                    if (!mover.CanMove)
                        failures.Add($"{proto}: can't move with a driver and key");
                    else if (moved.X < 2f)
                        failures.Add($"{proto}: barely moved driving right ({moved})");
                    else if (facing.X < 0.9f)
                        failures.Add($"{proto}: driving right but facing {facing}");
                    else if (speed <= walk)
                        failures.Add($"{proto}: only going {speed:F2} (walk {walk:F2})");

                    mover.HeldMoveButtons = MoveButtons.Up;
                });

                await pair.RunSeconds(2.5f);

                await server.WaitPost(() =>
                {
                    var facing = entMan.System<SharedTransformSystem>().GetWorldRotation(vehicle).ToWorldVec();
                    if (facing.Y < 0.9f)
                        failures.Add($"{proto}: steered up but facing {facing}");
                });
            }

            await server.WaitPost(() =>
            {
                if (entMan.EntityExists(driver))
                    entMan.DeleteEntity(driver);
                entMan.DeleteEntity(vehicle);
            });
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }
}
