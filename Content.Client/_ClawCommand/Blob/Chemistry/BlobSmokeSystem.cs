using System.Linq;
using Robust.Client.GameObjects;

namespace Content.Client._ClawCommand.Blob.Chemistry;

public sealed partial class BlobSmokeSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<Content.Shared._ClawCommand.Blob.Chemistry.BlobSmokeColorComponent, AfterAutoHandleStateEvent>(OnBlobTileHandleState);
    }

    private void OnBlobTileHandleState(EntityUid uid, Content.Shared._ClawCommand.Blob.Chemistry.BlobSmokeColorComponent component, ref AfterAutoHandleStateEvent state)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            _sprite.LayerSetColor((uid, sprite), i, component.Color);
        }
    }
}