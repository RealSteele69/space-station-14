using System.Linq;
using Content.Client.Inventory;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Cuffs;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._ClawCommand;

/// <summary>
/// Claw Command - Resomi draw on their own body sheet, so clothing must either use a hand-fitted
/// "-resomi" state or be warped by the Resomi displacement maps. Checks what the client actually renders.
/// </summary>
[TestFixture]
public sealed class ResomiVisualsTest : InteractionTest
{
    protected override string PlayerPrototype => "MobResomi";

    private const string PartsRsi = "_ClawCommand/Mobs/Species/Resomi/parts.rsi";

    [Test]
    public async Task ResomiBodyAndClothingRender()
    {
        var inventory = SEntMan.System<InventorySystem>();

        // Displacement-fitted slots, plus a coat and armour that ship a hand-fitted Resomi state.
        var outfit = new (string Proto, string Slot)[]
        {
            ("ClothingUniformJumpsuitColorGrey", "jumpsuit"),
            ("ClothingEyesGlassesMeson", "eyes"),
            ("ClothingMaskGas", "mask"),
            ("ClothingHeadHatBeret", "head"),
            ("ClothingShoesColorBlack", "shoes"),
            ("ClothingOuterWinterAtmos", "outerClothing"),
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

            // Body comes from the Resomi sheet, with the composited torso.
            Assert.That(spriteSys.TryGetLayer((CPlayer, sprite), HumanoidVisualLayers.Chest, out var chest, false));
            Assert.That(chest!.ActualRsi?.Path.ToString(), Does.EndWith(PartsRsi));
            Assert.That(chest.State.Name, Is.EqualTo("torso_m").Or.EqualTo("torso_f"));

            Assert.That(CEntMan.GetComponent<InventoryComponent>(CPlayer).SpeciesId, Is.EqualTo("resomi"));

            var slots = CEntMan.GetComponent<InventorySlotsComponent>(CPlayer);
            foreach (var (_, slot) in outfit)
            {
                Assert.That(slots.VisualLayerKeys.TryGetValue(slot, out var keys), $"{slot} rendered nothing");
                var displaced = keys!.Any(k => k.Contains("displacement", System.StringComparison.OrdinalIgnoreCase));

                if (slot == "outerClothing")
                {
                    // Hand-fitted state wins and skips displacement.
                    var layerKey = keys!.First(k => !k.Contains("displacement", System.StringComparison.OrdinalIgnoreCase));
                    Assert.That(spriteSys.TryGetLayer((CPlayer, sprite), layerKey, out var coat, false));
                    Assert.That(coat!.State.Name, Is.EqualTo("ATMOS-equipped-OUTERCLOTHING-resomi"));
                    Assert.That(displaced, Is.False, "fitted coat should not also be displaced");
                }
                else
                {
                    Assert.That(displaced, $"{slot} was not warped by a Resomi displacement map");
                }
            }
        });
    }

    [Test]
    public async Task ResomiCuffOverlayIsFitted()
    {
        var cuffs = SEntMan.System<SharedCuffableSystem>();

        await Server.WaitPost(() =>
        {
            var handcuffs = SEntMan.SpawnEntity("Handcuffs", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            Assert.That(cuffs.TryAddNewCuffs(SPlayer, SPlayer, handcuffs));
        });

        await RunTicks(30);

        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CPlayer);
            var spriteSys = CEntMan.System<SpriteSystem>();
            Assert.That(spriteSys.TryGetLayer((CPlayer, sprite), HumanoidVisualLayers.Handcuffs, out var layer, false));
            Assert.That(layer!.Visible);
            Assert.That(layer.State.Name, Does.EndWith("-resomi"));
        });
    }
}
