// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Focused test for Wraith's salt-line-banishment counter-play: dying once should revive the wraith (via
// BanishmentSystem) and reset its Wraith Points/regen rate (via WraithSystem.OnBanishment), while dying a
// second time should actually delete it (via WraithSystem.OnBanishmentDone). This exercises the
// interaction between BanishmentComponent, WraithPointsSystem and WraithSystem directly, independent of
// the full ghost-role antag-selection flow already covered by AntagGhostRoleTest.

using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._GoobStation.Wraith.Banishment;
using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.WraithPoints;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.GameRules;

[TestOf(typeof(BanishmentSystem))]
public sealed class WraithBanishmentTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestWraithBanishmentAbilities
  categories: [ HideSpawnMenu ]
  components:
  - type: Clickable

- type: entity
  id: TestWraithBanishment
  components:
  - type: Wraith
    abilities: TestWraithBanishmentAbilities
  - type: WraithPoints
    startingWraithPoints: 100
  - type: PassiveWraithPoints
    wpGeneration: 5
  - type: Banishment
    lives: 1
  - type: Damageable
  # Fork note: this fork splits raw damage storage (Damageable) from the component that actually
  # commits DamageDealtEvent into the dict and gates which damage types are supported (Injurable,
  # with DamageContainer=null meaning ""supports everything""). BaseMob always pairs the two - a
  # Damageable without an Injurable silently no-ops TryChangeDamage (it reports applied=true from
  # the computed delta, but the stored Damage dict stays empty and MobState never moves), which is
  # exactly the bug this test caught when it was missing here.
  - type: Injurable
  - type: MobState
    allowedStates:
    - Alive
    - Dead
  - type: MobThresholds
    thresholds:
      0: Alive
      10: Dead
";

    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = null!;
    [SidedDependency(Side.Server)] private readonly WraithPointsSystem _wraithPoints = null!;

    private static readonly DamageSpecifier Lethal = new()
    {
        DamageDict = new() { ["Blunt"] = 50 },
    };

    // Fork note: not [RunOnSide(Side.Server)] here - the second death goes through
    // WraithSystem.OnBanishmentDone's QueueDel, which only actually removes the entity on the next
    // tick's cleanup pass, not synchronously within the same WaitAssertion callback. Driving this
    // with explicit WaitAssertion/WaitRunTicks calls (the pattern used by e.g. SecretStartsTest and
    // StartEndGameRulesTest) lets a real tick run between the second death and the exists check.
    [Test]
    public async System.Threading.Tasks.Task DyingOnceRevivesAndResetsPoints_DyingTwiceDeletes()
    {
        EntityUid ent = default;

        await Server.WaitAssertion(() =>
        {
            ent = SSpawn("TestWraithBanishment");

            // Sanity: starts with its configured Wraith Points and one spare life.
            Assert.That(_wraithPoints.GetCurrentWp(ent).Int(), Is.EqualTo(100));
            Assert.That(SComp<BanishmentComponent>(ent).Lives, Is.EqualTo(1));

            // First death: BanishmentSystem should revive it (Rejuvenate) and consume its one spare
            // life, and WraithSystem.OnBanishment should reset its Wraith Points/regen rate to zero.
            var applied = _damageable.TryChangeDamage(ent, Lethal, ignoreResistances: true);
            Assert.That(applied, Is.True);
            Assert.That(SEntMan.EntityExists(ent), Is.True, "Wraith was deleted after its first death, but it had a spare life.");
            Assert.That(SComp<MobStateComponent>(ent).CurrentState, Is.EqualTo(MobState.Alive), "Banishment did not revive the wraith.");
            Assert.That(SComp<BanishmentComponent>(ent).Lives, Is.EqualTo(0), "Banishment did not consume the spare life.");
            Assert.That(_wraithPoints.GetCurrentWp(ent).Int(), Is.EqualTo(0), "WraithSystem did not reset Wraith Points on banishment.");

            // Second death: no lives left, so BanishmentDoneEvent should fire and
            // WraithSystem.OnBanishmentDone should queue the entity for deletion.
            _damageable.TryChangeDamage(ent, Lethal, ignoreResistances: true);
        });

        // Let the queued deletion actually process.
        await Server.WaitRunTicks(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(ent), Is.False, "Wraith survived a second death with no spare lives left.");
        });
    }
}
