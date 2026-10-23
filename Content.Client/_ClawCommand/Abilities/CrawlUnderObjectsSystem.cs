using Content.Shared._ClawCommand.Abilities;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._ClawCommand.Abilities;

/// <summary>
///     Claw Command - Draws a sneaking mob at small-mob depth so it renders underneath tables.
/// </summary>
public sealed partial class CrawlUnderObjectsSystem : SharedCrawlUnderObjectsSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrawlUnderObjectsComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<CrawlUnderObjectsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (args.TryGetData(SneakMode.Enabled, out bool enabled) && enabled)
        {
            if (ent.Comp.OriginalDrawDepth != null)
                return;

            ent.Comp.OriginalDrawDepth = sprite.DrawDepth;
            _sprite.SetDrawDepth((ent.Owner, sprite), (int) DrawDepth.SmallMobs);
        }
        else
        {
            if (ent.Comp.OriginalDrawDepth is not { } original)
                return;

            _sprite.SetDrawDepth((ent.Owner, sprite), original);
            ent.Comp.OriginalDrawDepth = null;
        }
    }
}
