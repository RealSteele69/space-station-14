// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Bingle;
using Robust.Client.GameObjects;

namespace Content.Client._ClawCommand.Bingle;

/// <summary>
/// Makes a bingle's eyes glow red while it is in combat mode.
/// </summary>
public sealed partial class BingleSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BingleComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<BingleComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_sprite.LayerMapTryGet((ent, args.Sprite), BingleVisual.Combat, out var layer, false))
            return;

        _appearance.TryGetData<bool>(ent, BingleVisual.Combat, out var combat, args.Component);
        _sprite.LayerSetVisible((ent, args.Sprite), layer, combat);
    }
}
