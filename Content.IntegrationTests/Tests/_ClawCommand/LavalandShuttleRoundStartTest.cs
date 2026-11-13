using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._ClawCommand.Lavaland.Procedural.Components;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Shared._ClawCommand.Lavaland.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Station.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - lavaland is generated while the round's maps load (before anyone spawns) and kept paused until
/// the round starts. Checks the mining shuttle still ends up docked at the station with both the station and
/// lavaland as destinations, and that it can actually fly there and back.
/// </summary>
[TestFixture]
public sealed class LavalandShuttleRoundStartTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [Test]
    public async Task MiningShuttleRoundStart()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var ticker = entMan.System<ServerGameTicker>();
        var shuttles = entMan.System<ShuttleSystem>();
        var mapSys = entMan.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            cfg.SetCVar(CCVars.GridFill, true);
            cfg.SetCVar(CCVars.GameMap, "Box");
            ticker.StartRound(true);
        });

        await pair.RunTicksSync(30);

        EntityUid shuttle = default;
        EntityUid stationGrid = default;
        EntityUid outpost = default;
        MapId stationMap = default;
        MapId lavalandMap = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            var lavalands = entMan.EntityQuery<LavalandMapComponent>(true).ToList();
            Assert.That(lavalands, Has.Count.EqualTo(1), "Box should generate one lavaland planet.");
            var lavaland = lavalands[0].Owner;
            lavalandMap = entMan.GetComponent<MapComponent>(lavaland).MapId;
            Assert.That(mapSys.IsPaused(lavalandMap), Is.False, "Lavaland should be unpaused once the round starts.");

            var outposts = entMan.EntityQuery<LavalandStationComponent>(true).ToList();
            Assert.That(outposts, Has.Count.EqualTo(1));
            outpost = outposts[0].Owner;

            var docking = new List<(EntityUid, DockingShuttleComponent)>();
            var query = entMan.AllEntityQueryEnumerator<DockingShuttleComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                docking.Add((uid, comp));
                var xform = entMan.GetComponent<TransformComponent>(uid);
                TestContext.Out.WriteLine($"shuttle {entMan.ToPrettyString(uid)} map={xform.MapID} paused={entMan.GetComponent<MetaDataComponent>(uid).EntityPaused} station={comp.Station} dests=[{string.Join(", ", comp.Destinations.Select(d => $"{d.Name}@{d.Map}"))}]");
            }

            Assert.That(docking, Has.Count.EqualTo(1), "Exactly one mining shuttle should exist.");
            var (uidShuttle, dockingComp) = docking[0];
            shuttle = uidShuttle;

            var stations = entMan.EntityQuery<StationDataComponent>(true).ToList();
            Assert.That(stations, Is.Not.Empty);
            stationGrid = stations[0].Grids.OrderByDescending(g => entMan.GetComponent<MapGridComponent>(g).LocalAABB.Size.LengthSquared()).First();
            stationMap = entMan.GetComponent<TransformComponent>(stationGrid).MapID;

            Assert.That(entMan.GetComponent<TransformComponent>(shuttle).MapID, Is.EqualTo(stationMap), "Mining shuttle should start docked at the station.");
            Assert.That(entMan.GetComponent<MetaDataComponent>(shuttle).EntityPaused, Is.False);
            Assert.That(dockingComp.Destinations.Select(d => d.Map), Does.Contain(stationMap), "Station must be an FTL destination.");
            Assert.That(dockingComp.Destinations.Select(d => d.Map), Does.Contain(lavalandMap), "Lavaland must be an FTL destination.");

            var consoles = entMan.AllEntityQueryEnumerator<DockingConsoleComponent, TransformComponent>();
            while (consoles.MoveNext(out var uid, out var console, out var xform))
            {
                TestContext.Out.WriteLine($"console {entMan.ToPrettyString(uid)} map={xform.MapID} hasShuttle={console.HasShuttle} shuttle={console.Shuttle}");
                Assert.That(console.Shuttle, Is.EqualTo(shuttle), $"{entMan.ToPrettyString(uid)} should be bound to the mining shuttle.");
            }
        });

        // Fly to lavaland the same way the console does.
        await server.WaitPost(() =>
            shuttles.FTLToDock(shuttle, entMan.GetComponent<ShuttleComponent>(shuttle), outpost, priorityTag: "DockMining"));
        await pair.RunSeconds(60);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TransformComponent>(shuttle).MapID, Is.EqualTo(lavalandMap), "Mining shuttle should arrive at lavaland.");
            Assert.That(IsDocked(entMan, shuttle), Is.True, "Mining shuttle should dock at the outpost.");
        });

        // Wait out the FTL cooldown (the console shows "recharging" until the FTL component is gone).
        var inFtl = true;
        for (var i = 0; i < 30 && inFtl; i++)
        {
            await pair.RunSeconds(5);
            await server.WaitPost(() => inFtl = entMan.HasComponent<FTLComponent>(shuttle));
        }

        await server.WaitPost(() =>
            shuttles.FTLToDock(shuttle, entMan.GetComponent<ShuttleComponent>(shuttle), stationGrid, priorityTag: "DockMining"));
        await pair.RunSeconds(60);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TransformComponent>(shuttle).MapID, Is.EqualTo(stationMap), "Mining shuttle should arrive back at the station.");
            Assert.That(IsDocked(entMan, shuttle), Is.True, "Mining shuttle should dock at the station.");
        });
    }

    private static bool IsDocked(IEntityManager entMan, EntityUid shuttle)
    {
        var query = entMan.AllEntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (query.MoveNext(out _, out var dock, out var xform))
        {
            if (xform.GridUid == shuttle && dock.Docked)
                return true;
        }

        return false;
    }
}
