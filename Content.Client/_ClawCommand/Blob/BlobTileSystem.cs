using Content.Client.DamageState;
using Content.Shared._ClawCommand.Blob;
using Content.Shared._ClawCommand.Blob.Components;
using Robust.Client.GameObjects;

namespace Content.Client._ClawCommand.Blob;

public sealed class BlobTileSystem : SharedBlobTileSystem
{
    protected override void TryUpgrade(Entity<BlobTileComponent> target, Entity<BlobCoreComponent> core, EntityUid observer) { }
}

public sealed class BlobTileVisualizerSystem : VisualizerSystem<BlobTileComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobTileComponent, AfterAutoHandleStateEvent>(OnBlobTileHandleState);
    }

    private void UpdateAppearance(EntityUid id, BlobTileComponent tile, AppearanceComponent? appearance = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(id, ref appearance, ref sprite))
            return;

        foreach (var key in new []{ DamageStateVisualLayers.Base, DamageStateVisualLayers.BaseUnshaded })
        {
            if (!SpriteSystem.LayerMapTryGet((id, sprite), key, out var index, false))
                continue;

            SpriteSystem.LayerSetColor((id, sprite), index, tile.Color);
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, BlobTileComponent component, ref AppearanceChangeEvent args)
    {
        UpdateAppearance(uid, component, args.Component, args.Sprite);
    }

    private void OnBlobTileHandleState(EntityUid uid, BlobTileComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateAppearance(uid, component);
    }
}