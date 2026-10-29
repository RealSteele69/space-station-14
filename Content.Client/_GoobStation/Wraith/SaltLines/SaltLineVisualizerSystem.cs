// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.SaltLines;
using Robust.Client.GameObjects;

namespace Content.Client._GoobStation.Wraith.SaltLines;

public sealed partial class SaltLineVisualizerSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearanceSystem = default!;
    [Dependency] private SpriteSystem _spriteSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SaltLineVisualizerComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<SaltLineVisualizerComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearanceSystem.TryGetData<SaltLineVisDirFlags>(ent.Owner, SaltLineVisuals.ConnectedMask, out var mask, args.Component))
            mask = SaltLineVisDirFlags.None;

        _spriteSystem.LayerSetRsiState((ent.Owner, args.Sprite), 0, $"{ent.Comp.State}{(int)mask}");
    }
}
