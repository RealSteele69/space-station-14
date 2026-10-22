using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._ClawCommand.AdvancedXenos;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.Buckle;
using Content.Shared.Doors.Systems;
using Content.Shared.Foldable;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - smoke tests for the advanced xenomorphs: the hugger-to-embryo-to-chestburster chain,
/// egg hatching, weed spreading, larva evolution and plasma spending.
/// </summary>
[TestFixture]
public sealed class AdvXenoTest : GameTest
{
    private static void FloorArea(IEntityManager entMan, ITileDefinitionManager tileDefs, Entity<MapGridComponent> grid, int radius)
    {
        var map = entMan.System<SharedMapSystem>();
        var plating = new Tile(tileDefs["Plating"].TileId);
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                map.SetTile(grid, new Vector2i(x, y), plating);
            }
        }
    }

    [Test]
    public async Task OnlyOrganicPlayerSpeciesCanHost()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var huggers = SEntMan.System<AdvXenoFacehuggerSystem>();
            foreach (var (proto, expected) in new[]
                     {
                         ("MobHuman", true), ("MobMoth", true), ("MobAvali", true),
                         ("MobIPC", false), ("MobSynth", false), ("BorgChassisGeneric", false),
                         ("MobCorgi", false), ("MobMonkey", false),
                     })
            {
                var mob = SEntMan.SpawnEntity(proto, testMap.GridCoords);
                Assert.That(huggers.IsValidHost(mob), Is.EqualTo(expected), $"{proto} host eligibility");

                var hugger = SEntMan.SpawnEntity("AdvXenoFacehugger", testMap.GridCoords);
                Assert.That(huggers.TryAttach(hugger, mob), Is.EqualTo(expected), $"{proto} hug result");
            }
        });
    }

    [Test]
    public async Task HuggerImplantsAndBursts()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid hugger = default;

        await server.WaitAssertion(() =>
        {
            human = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            hugger = entMan.SpawnEntity("AdvXenoFacehugger", testMap.GridCoords);
            var huggers = entMan.System<AdvXenoFacehuggerSystem>();

            Assert.That(huggers.CanHug(human), Is.True, "A bare-faced human should be huggable.");
            Assert.That(huggers.TryAttach(hugger, human), Is.True, "The hugger should latch on.");

            var inventory = entMan.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(human, "mask", out var worn) && worn == hugger, "The hugger should be worn as a mask.");
        });

        // MAX_IMPREGNATION_TIME is 15 seconds
        await pair.RunSeconds(16);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<AdvXenoEmbryoComponent>(human), "The host should carry an embryo.");
            Assert.That(entMan.GetComponent<AdvXenoFacehuggerComponent>(hugger).State, Is.EqualTo(AdvXenoHuggerState.Impregnated));

            // skip to the end of gestation
            var embryo = entMan.GetComponent<AdvXenoEmbryoComponent>(human);
            embryo.Stage = 5;
        });

        // 50% per two-second tick to start bursting, then 0.6s to finish; give it plenty of rolls
        await pair.RunSeconds(40);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(human) || !entMan.HasComponent<AdvXenoEmbryoComponent>(human), "The host should have burst.");
            var larvae = entMan.EntityQuery<AdvXenoLarvaComponent>().ToList();
            Assert.That(larvae, Is.Not.Empty, "A larva should have come out.");
            Assert.That(larvae.All(l => entMan.HasComponent<Content.Server.Ghost.Roles.Components.GhostRoleComponent>(l.Owner)), Is.True,
                "A chestburster should be offered to ghosts.");
        });
    }

    [Test]
    public async Task HelmetStopsHugger()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var helmet = entMan.SpawnEntity("ClothingHeadHelmetEVA", testMap.GridCoords);
            var inventory = entMan.System<InventorySystem>();
            Assert.That(inventory.TryEquip(human, helmet, "head", force: true), Is.True);

            var hugger = entMan.SpawnEntity("AdvXenoFacehugger", testMap.GridCoords);
            var huggers = entMan.System<AdvXenoFacehuggerSystem>();
            Assert.That(huggers.CanHug(human), Is.False, "A sealed helmet should make the host unhuggable.");
            Assert.That(huggers.TryAttach(hugger, human), Is.False);
            Assert.That(entMan.GetComponent<AdvXenoFacehuggerComponent>(hugger).State, Is.EqualTo(AdvXenoHuggerState.Dead), "The hugger smashes into the helmet and dies.");
        });
    }

    [Test]
    public async Task RiotVisorStopsHugger()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var helmet = entMan.SpawnEntity("ClothingHeadHelmetRiot", testMap.GridCoords);
            var inventory = entMan.System<InventorySystem>();
            Assert.That(inventory.TryEquip(human, helmet, "head", force: true), Is.True);

            var huggers = entMan.System<AdvXenoFacehuggerSystem>();
            Assert.That(huggers.CanHug(human), Is.False, "A lowered riot visor should make the host unhuggable.");

            // like a welding helmet: visor up, the face is open
            var foldable = entMan.System<FoldableSystem>();
            foldable.SetFolded(helmet, entMan.GetComponent<FoldableComponent>(helmet), true);
            Assert.That(huggers.CanHug(human), Is.True, "A raised riot visor should not protect.");

            foldable.SetFolded(helmet, entMan.GetComponent<FoldableComponent>(helmet), false);
            var hugger = entMan.SpawnEntity("AdvXenoFacehugger", testMap.GridCoords);
            Assert.That(huggers.TryAttach(hugger, human), Is.False);
            Assert.That(entMan.GetComponent<AdvXenoFacehuggerComponent>(hugger).State, Is.EqualTo(AdvXenoHuggerState.Dead), "The hugger smashes into the visor and dies.");
        });
    }

    [Test]
    public async Task WeldingMaskStopsHuggerButGasMaskDoesNot()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var inventory = entMan.System<InventorySystem>();
            var huggers = entMan.System<AdvXenoFacehuggerSystem>();

            var welder = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var weldingMask = entMan.SpawnEntity("ClothingMaskWeldingGas", testMap.GridCoords);
            Assert.That(inventory.TryEquip(welder, weldingMask, "mask", force: true), Is.True);
            Assert.That(huggers.CanHug(welder), Is.False, "A welding gas mask should make the host unhuggable.");

            var hugger = entMan.SpawnEntity("AdvXenoFacehugger", testMap.GridCoords);
            Assert.That(huggers.TryAttach(hugger, welder), Is.False);
            Assert.That(entMan.GetComponent<AdvXenoFacehuggerComponent>(hugger).State, Is.EqualTo(AdvXenoHuggerState.Dead), "The hugger smashes into the face shield and dies.");
            Assert.That(inventory.TryGetSlotEntity(welder, "mask", out var stillWorn) && stillWorn == weldingMask, "The welding mask stays on.");

            // an ordinary gas mask is just torn off
            var victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var gasMask = entMan.SpawnEntity("ClothingMaskGas", testMap.GridCoords);
            Assert.That(inventory.TryEquip(victim, gasMask, "mask", force: true), Is.True);
            Assert.That(huggers.CanHug(victim), Is.True, "A plain gas mask should not protect.");
        });
    }

    [Test]
    public async Task EggHatchesForNearbyHost()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid egg = default;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            FloorArea(entMan, tileDefs, testMap.Grid, 3);
            egg = entMan.SpawnEntity("AdvXenoEggGrown", testMap.GridCoords);
            human = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
        });

        await pair.RunSeconds(4);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<AdvXenoEggComponent>(egg).Status, Is.EqualTo(AdvXenoEggStatus.Burst));
            var inventory = entMan.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(human, "mask", out var worn) && entMan.HasComponent<AdvXenoFacehuggerComponent>(worn),
                "The child should have leapt onto the nearby host.");
        });
    }

    [Test]
    public async Task WeedsSpreadAndLarvaEvolves()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid larva = default;

        await server.WaitAssertion(() =>
        {
            FloorArea(entMan, tileDefs, testMap.Grid, 5);
            entMan.SpawnEntity("AdvXenoWeedNode", testMap.GridCoords);
            larva = entMan.SpawnEntity("MobAdvXenoLarva", testMap.GridCoords);
        });

        await pair.RunSeconds(12);

        await server.WaitAssertion(() =>
        {
            var weeds = entMan.EntityQuery<AdvXenoWeedsComponent>().Count();
            Assert.That(weeds, Is.GreaterThan(5), "Weeds should have spread out from the node.");

            var growth = entMan.GetComponent<AdvXenoLarvaComponent>(larva);
            Assert.That(growth.Growth, Is.GreaterThan(6), "A larva on weeds grows twice as fast.");

            growth.Growth = growth.MaxGrowth;
            var evolve = new AdvXenoEvolveActionEvent { Into = "MobAdvXenoDrone", RequiresGrowth = true, Performer = larva };
            entMan.EventBus.RaiseLocalEvent(larva, evolve);
            Assert.That(evolve.Handled, Is.True, "A grown larva should be able to evolve.");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(larva), Is.True);
            var drones = entMan.EntityQuery<AdvXenoCasteComponent>().Where(c => c.Caste == "drone").ToList();
            Assert.That(drones, Has.Count.EqualTo(1));
            var drone = drones[0].Owner;
            Assert.That(entMan.GetComponent<MetaDataComponent>(drone).EntityName, Does.StartWith("alien drone ("));

            // plasma: drones start with 200 and resin walls cost 75
            var life = entMan.System<AdvXenoLifeSystem>();
            var before = entMan.GetComponent<AdvXenoPlasmaComponent>(drone).Plasma;
            Assert.That(life.TryUsePlasma(drone, 75, false), Is.True);
            Assert.That(entMan.GetComponent<AdvXenoPlasmaComponent>(drone).Plasma, Is.EqualTo(before - 75));
            Assert.That(life.TryUsePlasma(drone, 10000, false), Is.False);
        });
    }

    [Test]
    public async Task ResinAcidAndDoor()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid drone = default;
        EntityUid human = default;
        EntityUid crate = default;

        await server.WaitAssertion(() =>
        {
            FloorArea(entMan, tileDefs, testMap.Grid, 4);
            drone = entMan.SpawnEntity("MobAdvXenoDrone", testMap.GridCoords);
            human = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new System.Numerics.Vector2(-1, 0)));
            var plasma = entMan.GetComponent<AdvXenoPlasmaComponent>(drone).Plasma;

            // Secrete Resin: a wall on the adjacent tile, 75 plasma
            var resin = new AdvXenoSecreteResinActionEvent
            {
                Prototype = "AdvXenoResinWall",
                Performer = drone,
                Target = testMap.GridCoords.Offset(new System.Numerics.Vector2(1, 0)),
            };
            entMan.EventBus.RaiseLocalEvent(drone, resin);
            Assert.That(resin.Handled, Is.True, "The drone should shape a resin wall.");
            Assert.That(entMan.GetComponent<AdvXenoPlasmaComponent>(drone).Plasma, Is.EqualTo(plasma - 75));
            Assert.That(entMan.EntityQuery<AdvXenoResinComponent>().Count(), Is.EqualTo(1));

            // Corrosive acid on a crate, wound forward to its last tick
            crate = entMan.SpawnEntity("CrateGenericSteel", testMap.GridCoords.Offset(new System.Numerics.Vector2(0, 1)));
            entMan.GetComponent<AdvXenoPlasmaComponent>(drone).Plasma = 500;
            var acid = new AdvXenoCorrosiveAcidActionEvent { Performer = drone, Target = crate };
            entMan.EventBus.RaiseLocalEvent(drone, acid);
            Assert.That(acid.Handled, Is.True, "The drone should be able to acid the crate.");
            foreach (var goop in entMan.EntityQuery<AdvXenoAcidComponent>())
            {
                goop.Ticks = goop.Strength - 1;
                goop.NextTick = TimeSpan.Zero;
            }

            var other = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new System.Numerics.Vector2(-1, -1)));

            // Resin door: opens for the hive only
            var door = entMan.SpawnEntity("AdvXenoResinDoor", testMap.GridCoords.Offset(new System.Numerics.Vector2(0, -1)));
            var doors = entMan.System<SharedDoorSystem>();
            Assert.That(doors.TryOpen(door, user: other), Is.False, "The resin door should not open for a human.");
            Assert.That(doors.TryOpen(door, user: drone), Is.True, "The resin door should open for the hive.");
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(crate) || entMan.IsQueuedForDeletion(crate), Is.True, "The acid should have eaten the crate.");
            Assert.That(entMan.EntityQuery<AdvXenoAcidComponent>().Count(), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task NestHoldsCaptive()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();
        EntityUid human = default;
        EntityUid rescuer = default;

        await server.WaitAssertion(() =>
        {
            FloorArea(entMan, tileDefs, testMap.Grid, 4);
            var buckle = entMan.System<SharedBuckleSystem>();
            var drone = entMan.SpawnEntity("MobAdvXenoDrone", testMap.GridCoords.Offset(new System.Numerics.Vector2(2.5f, 2.5f)));
            var nest = entMan.SpawnEntity("AdvXenoNest", testMap.GridCoords.Offset(new System.Numerics.Vector2(1.5f, 2.5f)));
            human = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new System.Numerics.Vector2(1.5f, 2.5f)));
            rescuer = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new System.Numerics.Vector2(1.5f, 1.5f)));

            Assert.That(buckle.TryBuckle(human, rescuer, nest), Is.False, "Only the hive may nest someone.");
            Assert.That(buckle.TryBuckle(human, drone, nest), Is.True, "The drone should secure the human.");
        });

        // past the buckle delay
        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            var buckle = entMan.System<SharedBuckleSystem>();
            Assert.That(buckle.TryUnbuckle(human, human), Is.False, "The captive has to struggle free.");
            Assert.That(buckle.IsBuckled(human), Is.True);
            Assert.That(buckle.TryUnbuckle(human, rescuer), Is.True, "Anyone else pulls them free at once.");
        });
    }
}
