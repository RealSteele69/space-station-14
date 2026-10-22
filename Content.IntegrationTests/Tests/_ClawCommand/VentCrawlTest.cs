using Content.IntegrationTests.Fixtures;
using System.Linq;
using Content.Client._ClawCommand.VentCrawl;
using Content.Server._ClawCommand.VentCrawl;
using Content.Shared.SubFloor;
using Robust.Client.Graphics;
using Content.Shared._ClawCommand.VentCrawl;
using Content.Shared.Cuffs;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - a larva crawls in one vent, along a pipe run, round a bend and out of another vent.
/// <code>
///   (3,2) vent B (faces south)
///   (3,1) pipe |
///   (0,0) vent A -- (1,0) pipe -- (2,0) pipe -- (3,0) bend
/// </code>
/// </summary>
[TestFixture]
public sealed class VentCrawlTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task CrawlAlongPipesAndOut()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid larva = default;
        EntityUid ventA = default;
        EntityUid ventB = default;
        EntityUid bend = default;

        await server.WaitAssertion(() =>
        {
            var map = entMan.System<SharedMapSystem>();
            var xformSys = entMan.System<SharedTransformSystem>();
            var plating = new Tile(tileDefs["Plating"].TileId);
            for (var x = -1; x <= 4; x++)
            for (var y = -1; y <= 3; y++)
                map.SetTile(testMap.Grid, new Vector2i(x, y), plating);

            EntityUid Place(string proto, int x, int y, Angle rot)
            {
                var uid = entMan.SpawnEntity(proto, map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(x, y)));
                xformSys.SetLocalRotation(uid, rot);
                return uid;
            }

            // Angle 0 is south; +90 degrees turns south into east.
            ventA = Place("GasVentPump", 0, 0, Angle.FromDegrees(90));
            Place("GasPipeStraight", 1, 0, Angle.FromDegrees(90));
            Place("GasPipeStraight", 2, 0, Angle.FromDegrees(90));
            bend = Place("GasPipeBend", 3, 0, Angle.FromDegrees(-90)); // south+west turned to west+north
            Place("GasPipeStraight", 3, 1, Angle.Zero);
            ventB = Place("GasVentPump", 3, 2, Angle.Zero);

            larva = entMan.SpawnEntity("MobAdvXenoLarva", map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(-1, 0)));
        });

        // let the pipe network form
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            entMan.System<VentCrawlSystem>().Enter(larva, ventA);
            Assert.That(entMan.HasComponent<VentCrawlingComponent>(larva), "The larva should be in the vent.");
            entMan.GetComponent<InputMoverComponent>(larva).HeldMoveButtons = MoveButtons.Right;
        });

        await pair.RunSeconds(3);

        await server.WaitAssertion(() =>
        {
            var crawling = entMan.GetComponent<VentCrawlingComponent>(larva);
            Assert.That(crawling.Pipe, Is.EqualTo(bend), "Holding right should follow the run to the bend and stop there.");
            entMan.GetComponent<InputMoverComponent>(larva).HeldMoveButtons = MoveButtons.Up;
        });

        // up the riser and into vent B; stop holding the key the moment it is out
        var exited = false;
        for (var i = 0; i < 120 && !exited; i++)
        {
            await pair.RunTicksSync(1);
            await server.WaitPost(() =>
            {
                if (entMan.HasComponent<VentCrawlingComponent>(larva))
                    return;
                exited = true;
                entMan.GetComponent<InputMoverComponent>(larva).HeldMoveButtons = MoveButtons.None;
            });
        }

        await server.WaitAssertion(() =>
        {
            Assert.That(exited, Is.True, "Crawling into vent B should climb out.");

            var map = entMan.System<SharedMapSystem>();
            var tile = map.TileIndicesFor(testMap.Grid, testMap.Grid, entMan.GetComponent<TransformComponent>(larva).Coordinates);
            Assert.That(tile, Is.EqualTo(new Vector2i(3, 2)), "The larva should come out of vent B.");
            Assert.That(entMan.EntityQuery<VentCrawlDuctComponent>(), Is.Empty, "The duct carrier should be cleaned up.");
            _ = ventB;
        });
    }

    /// <summary>The other ventcrawlers: mice, rats, roaches, raccoons, xenobio slimes, monkeys and kobolds.</summary>
    private static readonly string[] Critters =
    [
        "MobMouse", "MobRatServant", "MobCockroach", "MobMothroach", "MobRaccoon",
        "MobAdultSlimesBlue", "MobMonkey", "MobKobold",
    ];

    [Test]
    public async Task CrittersCanCrawl()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid ventA = default;
        EntityCoordinates start = default;

        await server.WaitAssertion(() =>
        {
            var map = entMan.System<SharedMapSystem>();
            var xformSys = entMan.System<SharedTransformSystem>();
            var plating = new Tile(tileDefs["Plating"].TileId);
            for (var x = -1; x <= 3; x++)
                map.SetTile(testMap.Grid, new Vector2i(x, 0), plating);

            ventA = entMan.SpawnEntity("GasVentPump", map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(0, 0)));
            xformSys.SetLocalRotation(ventA, Angle.FromDegrees(90));
            var pipe = entMan.SpawnEntity("GasPipeStraight", map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(1, 0)));
            xformSys.SetLocalRotation(pipe, Angle.FromDegrees(90));
            var ventB = entMan.SpawnEntity("GasVentPump", map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(2, 0)));
            xformSys.SetLocalRotation(ventB, Angle.FromDegrees(-90));
            start = map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(-1, 0));
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var verbs = entMan.System<SharedVerbSystem>();
            var crawl = entMan.System<VentCrawlSystem>();
            var verbText = Loc.GetString("vent-crawl-verb");
            foreach (var proto in Critters)
            {
                var mob = entMan.SpawnEntity(proto, start);
                Assert.That(entMan.HasComponent<VentCrawlerComponent>(mob), $"{proto} should be a ventcrawler.");
                Assert.That(verbs.GetLocalVerbs(ventA, mob, typeof(AlternativeVerb)).Any(v => v.Text == verbText),
                    $"{proto} should get the crawl verb on an adjacent vent.");
                Assert.That(crawl.TryStartEnter(mob, ventA), $"{proto} should be able to start climbing in.");
                entMan.DeleteEntity(mob);
            }
        });

        // a mouse goes all the way through
        EntityUid mouse = default;
        await server.WaitPost(() =>
        {
            mouse = entMan.SpawnEntity("MobMouse", start);
            entMan.System<VentCrawlSystem>().Enter(mouse, ventA);
            entMan.GetComponent<InputMoverComponent>(mouse).HeldMoveButtons = MoveButtons.Right;
        });

        var exited = false;
        for (var i = 0; i < 120 && !exited; i++)
        {
            await pair.RunTicksSync(1);
            await server.WaitPost(() =>
            {
                if (entMan.HasComponent<VentCrawlingComponent>(mouse))
                    return;
                exited = true;
                entMan.GetComponent<InputMoverComponent>(mouse).HeldMoveButtons = MoveButtons.None;
            });
        }

        await server.WaitAssertion(() =>
        {
            Assert.That(exited, "The mouse should crawl out of the far vent.");
            var map = entMan.System<SharedMapSystem>();
            var tile = map.TileIndicesFor(testMap.Grid, testMap.Grid, entMan.GetComponent<TransformComponent>(mouse).Coordinates);
            Assert.That(tile, Is.EqualTo(new Vector2i(2, 0)));
        });

        // ventcrawl_carry(): a dressed monkey has to strip first, and a cuffed one can't go at all
        await server.WaitAssertion(() =>
        {
            var crawl = entMan.System<VentCrawlSystem>();

            var dressed = entMan.SpawnEntity("MobMonkey", start);
            var hat = entMan.SpawnEntity("ClothingHeadHatBeret", start);
            Assert.That(entMan.System<InventorySystem>().TryEquip(dressed, hat, "head", silent: true, force: true));
            Assert.That(crawl.TryStartEnter(dressed, ventA), Is.False, "A monkey wearing a hat should not fit.");

            var cuffed = entMan.SpawnEntity("MobMonkey", start);
            var cuffs = entMan.SpawnEntity("Handcuffs", start);
            Assert.That(entMan.System<SharedCuffableSystem>().TryAddNewCuffs(cuffed, cuffed, cuffs));
            Assert.That(crawl.TryStartEnter(cuffed, ventA), Is.False, "A cuffed monkey should not be able to climb in.");
        });
    }

    /// <summary>A player crawling sees the ducts: the overlay is up and nearby pipes are revealed client-side.</summary>
    [Test]
    public async Task CrawlerSeesTheDucts()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid larva = default;
        EntityUid vent = default;
        EntityUid pipe = default;

        await server.WaitAssertion(() =>
        {
            var map = entMan.System<SharedMapSystem>();
            var xformSys = entMan.System<SharedTransformSystem>();
            var plating = new Tile(tileDefs["Plating"].TileId);
            for (var x = -1; x <= 3; x++)
                map.SetTile(testMap.Grid, new Vector2i(x, 0), plating);

            vent = entMan.SpawnEntity("GasVentPump", map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(0, 0)));
            xformSys.SetLocalRotation(vent, Angle.FromDegrees(90));
            pipe = entMan.SpawnEntity("GasPipeStraight", map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(1, 0)));
            xformSys.SetLocalRotation(pipe, Angle.FromDegrees(90));
            var cap = entMan.SpawnEntity("GasVentPump", map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(2, 0)));
            xformSys.SetLocalRotation(cap, Angle.FromDegrees(-90));

            larva = entMan.SpawnEntity("MobAdvXenoLarva", map.GridTileToLocal(testMap.Grid, testMap.Grid, new Vector2i(-1, 0)));
            var session = server.PlayerMan.GetSessionById(client.Session!.UserId);
            server.PlayerMan.SetAttachedEntity(session, larva);
        });

        await pair.RunTicksSync(15);
        await server.WaitPost(() => entMan.System<VentCrawlSystem>().Enter(larva, vent));
        await pair.RunTicksSync(30);

        var cEntMan = client.ResolveDependency<IEntityManager>();
        await client.WaitAssertion(() =>
        {
            Assert.That(client.ResolveDependency<IOverlayManager>().HasOverlay<VentCrawlOverlay>(), "The crawler's view should be up.");
            var cPipe = cEntMan.GetEntity(entMan.GetNetEntity(pipe));
            Assert.That(cEntMan.System<SharedAppearanceSystem>().TryGetData<bool>(cPipe, SubFloorVisuals.ScannerRevealed, out var revealed) && revealed,
                "Pipes around the crawler should be revealed.");
        });

        await server.WaitPost(() => entMan.GetComponent<InputMoverComponent>(larva).HeldMoveButtons = MoveButtons.Right);
        await pair.RunSeconds(1.5f);
        await server.WaitPost(() => entMan.GetComponent<InputMoverComponent>(larva).HeldMoveButtons = MoveButtons.None);
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<VentCrawlingComponent>(larva), Is.False, "The larva should be out of the far vent.");
            Assert.That(client.ResolveDependency<IOverlayManager>().HasOverlay<VentCrawlOverlay>(), Is.False, "The duct view should go away.");
        });
    }
}
