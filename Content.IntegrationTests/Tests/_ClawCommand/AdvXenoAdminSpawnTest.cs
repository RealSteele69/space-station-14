#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._ClawCommand.AdvancedXenos.Rules;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Roles;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - an admin-spawned larva: the ghost that takes it becomes a full xeno antagonist in place.
/// </summary>
[TestFixture]
public sealed class AdvXenoAdminSpawnTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true, DummyTicker = false, Connected = true };

    [Test]
    public async Task TakingAdminLarvaMakesAntag()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var session = playerMan.Sessions.Single();

        EntityUid larva = default;
        await server.WaitPost(() => larva = entMan.SpawnEntity("MobAdvXenoLarvaAdmin", map.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var id = entMan.GetComponent<GhostRoleComponent>(larva).Identifier;
            Assert.That(entMan.System<GhostRoleSystem>().Takeover(session, id), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(larva));
            Assert.That(entMan.GetComponent<TransformComponent>(larva).Coordinates, Is.EqualTo(map.GridCoords),
                "The larva should stay where the admin put it.");
            Assert.That(entMan.HasComponent<AdvXenoAdminSpawnComponent>(larva), Is.False);

            var mindId = session.ContentData()!.Mind!.Value;
            var mind = entMan.GetComponent<MindComponent>(mindId);
            Assert.That(mind.Objectives, Has.Count.EqualTo(2), "Survive and escape objectives.");

            var xenoRoles = mind.MindRoleContainer.ContainedEntities.Count(entMan.HasComponent<AdvXenomorphRoleComponent>);
            Assert.That(xenoRoles, Is.EqualTo(1), "Exactly one xeno mind role.");
            Assert.That(entMan.System<RoleSystem>().MindIsAntagonist(mindId), Is.True);

            var rules = entMan.EntityQuery<GameRuleComponent>()
                .Count(r => entMan.GetComponent<MetaDataComponent>(r.Owner).EntityPrototype?.ID == "AdvancedXenomorphAdmin");
            Assert.That(rules, Is.EqualTo(1));
        });
    }
}
