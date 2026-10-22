using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._ClawCommand.AdvancedXenos.Rules;
using Content.Server.GameTicking;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - the advanced xenomorph events on a real station: the NPC infestation and the midround larvae.
/// </summary>
[TestFixture]
public sealed class AdvXenoRulesTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true, DummyTicker = false, Map = "Dev" };

    private const string Infestation = "AdvancedXenoInfestation";
    private const string LarvaSpawn = "AdvancedXenomorphLarvaSpawn";

    [Test]
    public async Task InfestationSpawnsAtVents()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();

        EntityUid rule = default;
        await server.WaitAssertion(() => Assert.That(ticker.StartGameRule(Infestation, out rule), Is.True));

        // station events wait 10-20 seconds before they actually start
        await pair.RunSeconds(21);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<AdvXenoInfestationRuleComponent>(rule);
            Assert.That(comp.Vents, Is.Not.Empty, "The event should have picked an area with vents.");
        });

        await server.WaitAssertion(() => ticker.EndGameRule(rule));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityQuery<AdvXenoWeedNodeComponent>().Count(), Is.GreaterThan(0), "Weed nodes should sprout from the vents.");
            Assert.That(entMan.EntityQuery<AdvXenoBiomorphComponent>().Count(), Is.GreaterThan(0), "Xenomorphs should crawl out.");
        });
    }

    [Test]
    public async Task MidroundLarvaeComeThroughVents()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var ticker = entMan.System<GameTicker>();

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.StartGameRule(LarvaSpawn, out _), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var spawners = entMan.EntityQuery<GhostRoleComponent>()
                .Where(g => entMan.GetComponent<MetaDataComponent>(g.Owner).EntityPrototype?.ID == "SpawnPointGhostAdvXenoLarva")
                .ToList();
            Assert.That(spawners, Is.Not.Empty, "Larva ghost roles should have been placed.");
            foreach (var spawner in spawners)
            {
                Assert.That(entMan.GetComponent<TransformComponent>(spawner.Owner).MapID, Is.Not.EqualTo(Robust.Shared.Map.MapId.Nullspace),
                    "Larvae should come out of a vent, not nullspace.");
            }
        });
    }
}
