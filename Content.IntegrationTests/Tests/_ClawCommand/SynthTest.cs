using System.Collections.Generic;
using System.Linq;
using Content.Client.Inventory;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._ClawCommand.Silicons.Synths.Battery;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Preferences;
using Content.Shared.Storage.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - synths: the body sheet and fitted clothing render, and the power cell organ drains,
/// charges from food, shocks and recharging stations, while nanites slowly repair damage for hunger.
/// </summary>
[TestFixture]
public sealed class SynthTest : InteractionTest
{
    protected override string PlayerPrototype => "MobSynth";

    private const string PartsRsi = "_ClawCommand/Mobs/Species/Synth/parts.rsi";

    private int Seconds(float seconds) => (int) (seconds * Server.ResolveDependency<IGameTiming>().TickRate);

    [Test]
    public async Task SynthBodyAndClothingRender()
    {
        var inventory = SEntMan.System<InventorySystem>();

        var outfit = new (string Proto, string Slot)[]
        {
            ("ClothingUniformJumpsuitColorGrey", "jumpsuit"),
            ("ClothingMaskBreath", "mask"),
            ("ClothingHeadHatBeret", "head"),
            ("ClothingShoesColorBlack", "shoes"),
        };

        await Server.WaitPost(() =>
        {
            foreach (var (proto, slot) in outfit)
            {
                var item = SEntMan.SpawnEntity(proto, SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
                Assert.That(inventory.TryEquip(SPlayer, item, slot, silent: true, force: true), $"could not equip {proto} into {slot}");
            }
        });

        await RunTicks(30);

        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CPlayer);
            var spriteSys = CEntMan.System<SpriteSystem>();

            Assert.That(spriteSys.TryGetLayer((CPlayer, sprite), HumanoidVisualLayers.Chest, out var chest, false));
            Assert.That(chest!.ActualRsi?.Path.ToString(), Does.EndWith(PartsRsi));
            Assert.That(chest.State.Name, Is.EqualTo("torso"));

            Assert.That(spriteSys.TryGetLayer((CPlayer, sprite), HumanoidVisualLayers.Head, out var head, false));
            Assert.That(head!.State.Name, Is.EqualTo("head"));

            Assert.That(CEntMan.GetComponent<InventoryComponent>(CPlayer).SpeciesId, Is.EqualTo("synth"));

            var slots = CEntMan.GetComponent<InventorySlotsComponent>(CPlayer);
            foreach (var (_, slot) in outfit)
            {
                Assert.That(slots.VisualLayerKeys.TryGetValue(slot, out var keys), $"{slot} rendered nothing");
                var displaced = keys!.Any(k => k.Contains("displacement", System.StringComparison.OrdinalIgnoreCase));

                if (slot == "mask")
                {
                    // The breath mask ships a hand-fitted synth state, which wins over displacement.
                    var layerKey = keys!.First(k => !k.Contains("displacement", System.StringComparison.OrdinalIgnoreCase));
                    Assert.That(spriteSys.TryGetLayer((CPlayer, sprite), layerKey, out var mask, false));
                    Assert.That(mask!.State.Name, Is.EqualTo("equipped-MASK-synth"));
                    Assert.That(displaced, Is.False, "fitted mask should not also be displaced");
                }
                else
                {
                    Assert.That(displaced, $"{slot} was not warped by a synth displacement map");
                }
            }
        });
    }

    [Test]
    public async Task SynthDefaultMarkingsRender()
    {
        await Server.WaitPost(() =>
        {
            SEntMan.System<HumanoidProfileSystem>().ApplyProfileTo(SPlayer,
                HumanoidCharacterProfile.DefaultWithSpecies("SpeciesSynth"));
        });

        await RunTicks(30);

        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CPlayer);
            var drawn = sprite.AllLayers
                .Where(l => l.Visible && l.ActualRsi != null)
                .Select(l => $"{l.ActualRsi!.Path}:{l.RsiState.Name}")
                .ToList();

            // The required head, snout and tail layers start with the factory visor, jaw, snout and tail.
            foreach (var expected in new[]
                     {
                         "Synth/head.rsi:visor", "Synth/head.rsi:jaw_composite", "Synth/snout.rsi:nose",
                         "Synth/tail.rsi:factory",
                     })
            {
                Assert.That(drawn.Any(d => d.Contains(expected)), $"default marking {expected} isn't drawn; drawn: {string.Join(", ", drawn)}");
            }
        });
    }

    [Test]
    public async Task SynthPowerCell()
    {
        var synthBattery = SEntMan.System<SynthBatterySystem>();
        var battery = SEntMan.System<SharedBatterySystem>();
        var damageable = SEntMan.System<DamageableSystem>();
        var effects = SEntMan.System<SharedEntityEffectsSystem>();

        Entity<BatteryComponent> cell = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(synthBattery.TryGetBattery(SPlayer, out var found), "synth has no power cell organ");
            cell = found!.Value;
            Assert.That(SEntMan.GetComponent<OrganComponent>(cell).Body, Is.EqualTo(SPlayer));
            battery.SetCharge(cell.AsNullable(), 360f);
        });

        // Drains over time.
        await RunTicks(Seconds(5));
        float charge = 0;
        await Server.WaitAssertion(() =>
        {
            charge = battery.GetCharge(cell.AsNullable());
            Assert.That(charge, Is.LessThan(360f).And.GreaterThan(350f), "cell should drain about 0.4/s");
        });

        // Eating charges it.
        await Server.WaitAssertion(() =>
        {
            effects.ApplyEffect(SPlayer, new Satiate { SatiationType = SatiationSystem.Hunger, Factor = 10f });
            Assert.That(battery.GetCharge(cell.AsNullable()), Is.EqualTo(charge + 24f).Within(1f), "food should charge the cell");
            charge = battery.GetCharge(cell.AsNullable());
        });

        // Shocks charge it too, and fry a little.
        await Server.WaitAssertion(() =>
        {
            var shock = new DamageSpecifier();
            shock.DamageDict["Shock"] = FixedPoint2.New(5);
            damageable.TryChangeDamage(SPlayer, shock, origin: SPlayer);

            // 5 shock * 1.4 vulnerability * 10.8 charge per point
            Assert.That(battery.GetCharge(cell.AsNullable()), Is.EqualTo(charge + 75.6f).Within(1f), "shock should charge the cell");
            Assert.That(damageable.GetPositiveDamage((SPlayer, SEntMan.GetComponent<Content.Shared.Damage.Components.DamageableComponent>(SPlayer))).DamageDict
                .GetValueOrDefault("Cellular"), Is.GreaterThan(FixedPoint2.Zero), "shock should cause cellular damage");
        });

        // A flat cell slows the synth down.
        await Server.WaitPost(() => battery.SetCharge(cell.AsNullable(), 0f));
        await RunTicks(Seconds(2));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SynthBatteryComponent>(SPlayer).Unpowered, "synth should be unpowered on an empty cell");
        });

        // A cyborg recharging station charges the cell while the synth sits inside.
        EntityUid station = default;
        await Server.WaitPost(() =>
        {
            station = SEntMan.SpawnEntity("BorgCharger", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            SEntMan.GetComponent<ChargerComponent>(station).Portable = true; // no power grid on the test map
            Assert.That(SEntMan.System<SharedEntityStorageSystem>().Insert(SPlayer, station), "synth didn't fit in the recharging station");
        });
        await RunTicks(Seconds(3));
        await Server.WaitAssertion(() =>
        {
            Assert.That(battery.GetCharge(cell.AsNullable()), Is.GreaterThan(30f), "recharging station should charge the synth");
            Assert.That(SEntMan.GetComponent<SynthBatteryComponent>(SPlayer).Unpowered, Is.False);
        });
    }

    [Test]
    public async Task SynthSelfRepair()
    {
        var damageable = SEntMan.System<DamageableSystem>();
        var satiation = SEntMan.System<Content.Server.Nutrition.EntitySystems.ServerSatiationSystem>();

        await Server.WaitPost(() =>
        {
            // The test map is a vacuum; keep the environment from adding damage of its own.
            SEntMan.RemoveComponent<Content.Server.Atmos.Components.BarotraumaComponent>(SPlayer);
            SEntMan.RemoveComponent<Content.Shared.Temperature.Components.TemperatureDamageComponent>(SPlayer);

            satiation.SetValue((SPlayer, SEntMan.GetComponent<SatiationComponent>(SPlayer)), SatiationSystem.Hunger, 150f);
            var blunt = new DamageSpecifier();
            blunt.DamageDict["Blunt"] = FixedPoint2.New(20);
            damageable.TryChangeDamage(SPlayer, blunt, origin: SPlayer);
        });

        await RunTicks(Seconds(10));

        await Server.WaitAssertion(() =>
        {
            var damage = damageable.GetPositiveDamage((SPlayer, SEntMan.GetComponent<Content.Shared.Damage.Components.DamageableComponent>(SPlayer)));
            var blunt = damage.DamageDict.GetValueOrDefault("Blunt");
            Assert.That(blunt, Is.LessThan(FixedPoint2.New(20)), "nanites should repair blunt damage");
            Assert.That(blunt, Is.GreaterThan(FixedPoint2.New(15)), "repairs should be slow");

            var hunger = satiation.GetValueOrNull((SPlayer, SEntMan.GetComponent<SatiationComponent>(SPlayer)), SatiationSystem.Hunger);
            Assert.That(hunger, Is.LessThan(149f), "repairs should cost hunger");
        });
    }
}
