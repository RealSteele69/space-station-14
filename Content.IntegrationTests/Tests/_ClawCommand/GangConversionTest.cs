using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._ClawCommand.GangConversion.Rule;
using Content.Server.Decals;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared._ClawCommand.GangConversion.Components;
using Content.Shared._ClawCommand.GangConversion.Roles;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - Gang Conversion: boss selection founds rival gangs with claimed gear, the tattoo gun
/// forcibly recruits (and can flip a rival's member), and the spray tagger's decals count as turf.
/// </summary>
[TestFixture]
public sealed class GangConversionTest : GameTest
{
    private const string GangGameRuleProtoId = "GangConversion";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
        Map = PoolManager.TestStation,
    };

    private const string GangBossAntagRoleName = "GangBoss";

    private async Task<(GangConversionRuleSystem RuleSys, Entity<GangConversionRuleComponent> Rule)> StartGangRound(int dummyCount = 6)
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var ruleSys = server.System<GangConversionRuleSystem>();

        var dummies = await server.AddDummySessions(dummyCount);
        await pair.RunTicksSync(5);

        // AssignAntag hard-filters on antag preference (no "not enough opted in" fallback), so every
        // candidate for the rule's LinearAntagCount slots needs to opt into GangBoss ahead of time -
        // same reason TraitorRuleTest opts its one real client in before starting the round.
        await pair.SetAntagPreference(GangBossAntagRoleName, true);
        foreach (var dummy in dummies)
        {
            await pair.SetAntagPreference(GangBossAntagRoleName, true, dummy.UserId);
        }

        GangConversionRuleComponent? rule = null;
        EntityUid ruleUid = default;
        await server.WaitPost(() =>
        {
            ruleUid = ticker.AddGameRule(GangGameRuleProtoId);
            Assert.That(entMan.TryGetComponent(ruleUid, out rule));

            ticker.ToggleReadyAll(true);
            ticker.StartRound();
            ticker.StartGameRule(ruleUid);
        });
        await pair.RunTicksSync(10);

        return (ruleSys, (ruleUid, rule!));
    }

    [Test]
    public async Task BossesFoundRivalGangsWithClaimedGear()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var mindSys = server.System<MindSystem>();
        var roleSys = server.System<SharedRoleSystem>();
        var handsSys = server.System<SharedHandsSystem>();

        var (ruleSys, rule) = await StartGangRound();

        await server.WaitAssertion(() =>
        {
            var teams = ruleSys.GetTeams(rule);
            Assert.That(teams.Count, Is.GreaterThanOrEqualTo(2), "At least two rival gangs should form.");

            var usedNames = new HashSet<string>();
            var usedColors = new HashSet<Color>();

            foreach (var team in teams)
            {
                Assert.That(usedNames.Add(team.Name), $"Gang name '{team.Name}' was reused.");
                Assert.That(usedColors.Add(team.Color), $"Gang colour for '{team.Name}' was reused.");

                Assert.That(team.Boss, Is.Not.Null, $"Gang '{team.Name}' has no boss.");
                var boss = team.Boss!.Value;

                Assert.That(entMan.HasComponent<GangBossComponent>(boss));
                Assert.That(entMan.TryGetComponent<GangMemberComponent>(boss, out var member));
                Assert.That(member!.GangId, Is.EqualTo(team.Id));
                Assert.That(team.Members, Does.Contain(boss));

                var mind = mindSys.GetMind(boss);
                Assert.That(mind, Is.Not.Null, "Boss should have a mind.");
                Assert.That(roleSys.MindHasRole<GangBossRoleComponent>(mind!.Value), "Boss should carry the gang boss mind role.");

                var held = handsSys.EnumerateHeld(boss).ToList();
                var gun = held.Select(i => entMan.TryGetComponent<GangConverterComponent>(i, out var c) ? c : null)
                    .FirstOrDefault(c => c != null);
                Assert.That(gun, Is.Not.Null, "Boss should start with a tattoo gun.");
                Assert.That(gun!.GangId, Is.EqualTo(team.Id), "The boss's tattoo gun should be claimed for their own gang.");

                var tagger = held.Select(i => entMan.TryGetComponent<GangTaggerComponent>(i, out var c) ? c : null)
                    .FirstOrDefault(c => c != null);
                Assert.That(tagger, Is.Not.Null, "Boss should start with a spray tagger.");
                Assert.That(tagger!.GangId, Is.EqualTo(team.Id));
                Assert.That(tagger!.Color, Is.EqualTo(team.Color));
            }
        });
    }

    [Test]
    public async Task TattooGunForciblyConvertsAndCanFlipARival()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var mindSys = server.System<MindSystem>();
        var roleSys = server.System<SharedRoleSystem>();
        var handsSys = server.System<SharedHandsSystem>();

        var (ruleSys, rule) = await StartGangRound();

        EntityUid bossA = default, bossB = default, target = default;
        int gangAId = -1, gangBId = -1;
        await server.WaitAssertion(() =>
        {
            var teams = ruleSys.GetTeams(rule);
            Assert.That(teams.Count, Is.GreaterThanOrEqualTo(2));

            var gangA = teams[0];
            var gangB = teams[1];
            gangAId = gangA.Id;
            gangBId = gangB.Id;
            bossA = gangA.Boss!.Value;
            bossB = gangB.Boss!.Value;

            var bosses = new HashSet<EntityUid> { bossA, bossB };
            var candidate = entMan.AllEntityUids<GangMemberComponent>().FirstOrDefault(e => !bosses.Contains(e));

            // Fall back to any humanoid that isn't a boss if nobody's been recruited yet.
            if (candidate == default)
            {
                candidate = entMan.AllEntityUids<Content.Shared.Humanoid.HumanoidProfileComponent>()
                    .First(e => !bosses.Contains(e));
            }

            target = candidate;
        });

        Assert.That(target, Is.Not.EqualTo(default(EntityUid)));

        await server.WaitAssertion(() =>
        {
            var gunA = handsSys.EnumerateHeld(bossA).First(i => entMan.HasComponent<GangConverterComponent>(i));
            var hitEvent = new MeleeHitEvent(new HashSet<EntityUid> { target }, bossA, gunA, new DamageSpecifier(), null);
            entMan.EventBus.RaiseLocalEvent(gunA, hitEvent);
        });
        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<GangMemberComponent>(target, out var member));
            Assert.That(member!.GangId, Is.EqualTo(gangAId), "Target should have been recruited into gang A.");

            var mind = mindSys.GetMind(target);
            Assert.That(mind, Is.Not.Null);
            Assert.That(roleSys.MindHasRole<GangMemberRoleComponent>(mind!.Value));

            var teams = ruleSys.GetTeams(rule);
            Assert.That(teams.First(t => t.Id == gangAId).Members, Does.Contain(target));
        });

        // Gang B's boss hits the same target with their own gun: they should flip, not stay in A.
        await server.WaitAssertion(() =>
        {
            var gunB = handsSys.EnumerateHeld(bossB).First(i => entMan.HasComponent<GangConverterComponent>(i));
            var hitEvent = new MeleeHitEvent(new HashSet<EntityUid> { target }, bossB, gunB, new DamageSpecifier(), null);
            entMan.EventBus.RaiseLocalEvent(gunB, hitEvent);
        });
        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            var member = entMan.GetComponent<GangMemberComponent>(target);
            Assert.That(member.GangId, Is.EqualTo(gangBId), "Target should have defected to gang B.");

            var teams = ruleSys.GetTeams(rule);
            Assert.That(teams.First(t => t.Id == gangAId).Members, Does.Not.Contain(target), "Gang A should have lost the defector.");
            Assert.That(teams.First(t => t.Id == gangBId).Members, Does.Contain(target), "Gang B should have gained the defector.");
        });
    }

    [Test]
    public async Task TaggedTurfCountsTowardTheTaggingGang()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var decals = server.System<DecalSystem>();

        var (ruleSys, rule) = await StartGangRound();

        await server.WaitAssertion(() =>
        {
            var teams = ruleSys.GetTeams(rule);
            Assert.That(teams.Count, Is.GreaterThanOrEqualTo(1));
            var gangA = teams[0];
            var boss = gangA.Boss!.Value;
            var coords = entMan.GetComponent<TransformComponent>(boss).Coordinates;
            var decalId = ruleSys.GetTagDecalId(rule);

            Assert.That(decals.TryAddDecal(decalId, coords, out _, gangA.Color, cleanable: true),
                "Should be able to drop a tag decal at the boss's position.");

            ruleSys.ForceTurfRecount(rule);

            var recounted = ruleSys.GetTeams(rule).First(t => t.Id == gangA.Id);
            Assert.That(recounted.TurfScore, Is.GreaterThanOrEqualTo(1), "The tagged decal should count as that gang's turf.");
        });
    }
}
