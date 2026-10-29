// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components;
using Robust.Client.GameObjects;

namespace Content.Client._GoobStation.Wraith;

public sealed partial class FadingInSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FadingInComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, FadingInComponent fading, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Start fully transparent
        _sprites.SetColor((uid, sprite), sprite.Color.WithAlpha(0f));
        fading.Elapsed = 0f;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<FadingInComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var fading, out var sprite))
        {
            if (fading.Finished)
                continue;

            fading.Elapsed += frameTime;

            var alpha = Math.Clamp(fading.Elapsed / fading.FadeInTime, 0f, 1f);
            _sprites.SetColor((uid, sprite), sprite.Color.WithAlpha(alpha));
        }
    }
}
