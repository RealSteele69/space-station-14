// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Minions.Harbinger;
using Robust.Client.GameObjects;

namespace Content.Client._GoobStation.Wraith;

/// <summary>
/// This handles appearance for shuffling
/// </summary>
public sealed partial class SpikerShuffleClientSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _spriteSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpikerShuffleComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<SpikerShuffleComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(ent.Owner, ShuffleVisuals.Shuffling, out var shuffling, args.Component))
        {
            if (shuffling)
                _spriteSystem.LayerSetRsiState((ent.Owner, args.Sprite), 0, ent.Comp.Shuffling);
            else
                _spriteSystem.LayerSetRsiState((ent.Owner, args.Sprite), 0, ent.Comp.Normal);
        }
    }
}
