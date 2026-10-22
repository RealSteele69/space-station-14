using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.Systems;
using Content.Shared._ClawCommand.Lavaland.Aggression;
using Content.Shared._ClawCommand.Lavaland.LavalandDeps;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Components;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Systems;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - checks that the lavaland megafauna AI actually wakes up, picks targets
/// and lands its attacks, and that the lavaland natives don't fight each other.
/// </summary>
[TestFixture]
public sealed class LavalandMegafaunaTest : GameTest
{
    private const string Target = "MobHuman";

    private static readonly string[] Bosses =
    {
        "MobHierophant",
        "LavalandBossAshDrake",
        "LavalandBossBubblegum",
        "LavalandBossColossus",
        "LavalandBossMegaLegion",
        "LavalandEliteLegionnaire",
        "LavalandEliteHerald",
        "LavalandEliteGoliathBroodmother",
        "LavalandElitePandora",
    };

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

    private static bool UseAttack(IEntityManager entMan, EntityUid boss, EntityUid target, string actionId)
    {
        var actions = entMan.System<SharedActionsSystem>();
        var megafauna = entMan.System<MegafaunaSystem>();
        if (!actions.TryGetActionById(boss, actionId, out var action))
            return false;

        var targeting = entMan.EnsureComponent<MegafaunaAiTargetingComponent>(boss);
        targeting.TargetEnt = target;
        targeting.TargetCoords = entMan.GetComponent<TransformComponent>(target).Coordinates;

        var ev = megafauna.GetPerformEvent(boss, action.Value.Owner);
        return actions.TryPerformAction(boss, ev);
    }

    [Test]
    [TestCaseSource(nameof(Bosses))]
    public async Task MegafaunaStartsAndStops(string bossId)
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        MegafaunaAiComponent ai = null!;

        await server.WaitPost(() =>
        {
            boss = entMan.SpawnAtPosition(bossId, testMap.GridCoords);
            ai = entMan.GetComponent<MegafaunaAiComponent>(boss);
        });

        await server.WaitRunTicks(5);

        Assert.That(ai.Active, Is.False);
        Assert.That(ai.Schedule, Is.Empty);

        await server.WaitAssertion(() => entMan.System<MegafaunaSystem>().StartupMegafauna((boss, ai)));
        await server.WaitRunTicks(1);

        Assert.That(ai.Active, Is.True);
        Assert.That(ai.Schedule, Is.Not.Empty);

        await server.WaitAssertion(() => entMan.System<MegafaunaSystem>().ShutdownMegafauna((boss, ai)));
        await server.WaitRunTicks(1);

        Assert.That(ai.Active, Is.False);
        Assert.That(ai.Schedule, Is.Empty);
    }

    [Test]
    [TestCaseSource(nameof(Bosses))]
    public async Task MegafaunaUsesAttacksOnAggressor(string bossId)
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;

        await server.WaitAssertion(() =>
        {
            FloorArea(entMan, tileDefs, testMap.Grid, 15);
            boss = entMan.SpawnAtPosition(bossId, testMap.GridCoords);
            var human = entMan.SpawnAtPosition(Target, testMap.GridCoords.Offset(new Vector2(4, 0)));

            var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
            entMan.System<AggressorsSystem>().AddAggressor((boss, aggressive), human);
            Assert.That(entMan.GetComponent<MegafaunaAiComponent>(boss).Active, Is.True, "Getting an aggressor should wake the AI up.");
        });

        await pair.RunSeconds(10);

        await server.WaitAssertion(() =>
        {
            var actions = entMan.System<SharedActionsSystem>();
            var used = actions.GetActions(boss).Count(a => a.Comp.Cooldown != null);
            Assert.That(used, Is.GreaterThan(0), $"{bossId} never used any of its attacks.");
        });
    }

    [Test]
    public async Task ChargePlowsThroughTarget()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            FloorArea(entMan, tileDefs, testMap.Grid, 15);
            boss = entMan.SpawnAtPosition("LavalandBossBubblegum", testMap.GridCoords);
            human = entMan.SpawnAtPosition(Target, testMap.GridCoords.Offset(new Vector2(6, 0)));
            entMan.System<NPCSystem>().SleepNPC(boss);

            Assert.That(UseAttack(entMan, boss, human, "ActionBubblegumCharge"), Is.True, "The charge should go off.");
            Assert.That(entMan.HasComponent<MegafaunaChargingComponent>(boss), Is.True);
        });

        await pair.RunSeconds(2.5f);

        await server.WaitAssertion(() =>
        {
            var xform = entMan.System<SharedTransformSystem>();
            var bossPos = xform.GetWorldPosition(boss);
            Assert.That(bossPos.X, Is.GreaterThan(4f), "Bubblegum should have dashed towards the target.");
            Assert.That(entMan.HasComponent<MegafaunaChargingComponent>(boss), Is.False, "The charge should be over.");

            var damage = entMan.System<DamageableSystem>().GetAllDamage(human).DamageDict;
            Assert.That(damage.TryGetValue("Slash", out var slash) && slash > 0, Is.True, "The charge should have hit the target.");
        });
    }

    [Test]
    public async Task SwoopLandsOnTarget()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;
        EntityUid human = default;
        var landingSpot = Vector2.Zero;

        await server.WaitAssertion(() =>
        {
            FloorArea(entMan, tileDefs, testMap.Grid, 15);
            boss = entMan.SpawnAtPosition("LavalandBossAshDrake", testMap.GridCoords);
            human = entMan.SpawnAtPosition(Target, testMap.GridCoords.Offset(new Vector2(7, 3)));
            landingSpot = entMan.System<SharedTransformSystem>().GetWorldPosition(human);
            entMan.System<NPCSystem>().SleepNPC(boss);
            // Godmode already blocks non-player damage; drop it so the airborne immunity is what's tested.
            entMan.RemoveComponent<MegafaunaGodmodeComponent>(boss);

            Assert.That(UseAttack(entMan, boss, human, "ActionAshDrakeSwoop"), Is.True, "The swoop should go off.");
            Assert.That(entMan.HasComponent<MegafaunaAirborneComponent>(boss), Is.True);
        });

        await pair.RunSeconds(0.5f);

        await server.WaitAssertion(() =>
        {
            // Nothing can hurt the drake while it's up in the air.
            var damageable = entMan.System<DamageableSystem>();
            var before = damageable.GetTotalDamage(boss);
            var hit = new Content.Shared.Damage.DamageSpecifier();
            hit.DamageDict.Add("Blunt", 50);
            damageable.TryChangeDamage(boss, hit, origin: human);
            Assert.That(damageable.GetTotalDamage(boss), Is.EqualTo(before), "An airborne boss should not take damage.");
        });

        await pair.RunSeconds(3f);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<MegafaunaAirborneComponent>(boss), Is.False, "The drake should have landed.");
            var xform = entMan.System<SharedTransformSystem>();
            var distance = (xform.GetWorldPosition(boss) - landingSpot).Length();
            Assert.That(distance, Is.LessThan(2f), "The drake should land where the target was standing.");
        });
    }

    [Test]
    public async Task ProjectileVolleyFires()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid boss = default;

        await server.WaitAssertion(() =>
        {
            FloorArea(entMan, tileDefs, testMap.Grid, 15);
            boss = entMan.SpawnAtPosition("LavalandBossColossus", testMap.GridCoords);
            var human = entMan.SpawnAtPosition(Target, testMap.GridCoords.Offset(new Vector2(8, 0)));
            entMan.System<NPCSystem>().SleepNPC(boss);

            Assert.That(UseAttack(entMan, boss, human, "ActionColossusBlast"), Is.True, "The blast should go off.");
        });

        await pair.RunSeconds(1.2f);

        await server.WaitAssertion(() =>
        {
            var bolts = 0;
            var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var meta))
            {
                if (meta.EntityPrototype?.ID == "MegafaunaProjectileColossusBolt")
                    bolts++;
            }

            // 8 per ring, at least two rings by now.
            Assert.That(bolts, Is.GreaterThanOrEqualTo(16));
        });
    }

    [Test]
    public async Task LavalandNativesAreAllies()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        string[] natives =
        {
            "MobLavalandGoliath",
            "MobLegion",
            "MobWatcherLavaland",
            "LavalandMobAshWhelp",
            "LavalandMobBrimdemon",
            "LavalandBossAshDrake",
            "LavalandBossBubblegum",
            "LavalandEliteHerald",
        };

        await server.WaitAssertion(() =>
        {
            var factions = entMan.System<NpcFactionSystem>();
            var spawned = natives.Select(id => entMan.SpawnAtPosition(id, testMap.GridCoords)).ToList();
            var human = entMan.SpawnAtPosition(Target, testMap.GridCoords);

            for (var i = 0; i < spawned.Count; i++)
            {
                var hostiles = factions.GetNearbyHostiles(spawned[i], 5f).ToHashSet();
                foreach (var other in spawned)
                {
                    Assert.That(hostiles, Does.Not.Contain(other),
                        $"{natives[i]} should not be hostile to {entMan.ToPrettyString(other)}.");
                }

                Assert.That(hostiles, Does.Contain(human), $"{natives[i]} should still hunt crew.");
            }
        });
    }
}
