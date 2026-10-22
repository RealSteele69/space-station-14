using Content.Client.Clothing;
using Content.Shared._ClawCommand.Body.Preening;
using Content.Shared.Clothing.Components;
using Robust.Client.GameObjects;

namespace Content.Client._ClawCommand.Body;

/// <summary>
///     Claw Command - Tints feathers (item sprite and worn sprite) with the colour of the creature they came
///     from, plus a blood overlay for feathers that were torn out.
/// </summary>
public sealed partial class FeatherVisualizerSystem : VisualizerSystem<FeatherComponent>
{
    [Dependency] private ClientClothingSystem _clothing = default!;

    protected override void OnAppearanceChange(EntityUid uid, FeatherComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<Color>(uid, FeatherVisuals.FeatherColor, out var featherColor, args.Component))
            return;

        SpriteSystem.LayerSetColor((uid, args.Sprite), FeatherVisualLayers.Feather, featherColor);

        if (TryComp<ClothingComponent>(uid, out var clothing))
        {
            foreach (var slot in clothing.ClothingVisuals.Keys)
            {
                _clothing.SetLayerColor(clothing, slot, "feather", featherColor);
            }
        }

        if (AppearanceSystem.TryGetData<Color>(uid, FeatherVisuals.BloodColor, out var bloodColor, args.Component))
            SpriteSystem.LayerSetColor((uid, args.Sprite), FeatherVisualLayers.Blood, bloodColor);
    }
}
