// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Bingle;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._ClawCommand.Bingle;

/// <summary>
/// Plays the shrinking fall animation for things dropping into a bingle pit.
/// </summary>
public sealed partial class BingleFallingVisualsSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _anim = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string FallAnimationKey = "bingle_pit_fall";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BinglePitFallingComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BinglePitFallingComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(Entity<BinglePitFallingComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || TerminatingOrDeleted(ent))
            return;

        ent.Comp.OriginalScale = sprite.Scale;

        var player = EnsureComp<AnimationPlayerComponent>(ent);
        if (_anim.HasRunningAnimation(player, FallAnimationKey))
            return;

        _anim.Play((ent, player), GetFallingAnimation(ent.Comp), FallAnimationKey);
    }

    private void OnComponentRemove(Entity<BinglePitFallingComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || TerminatingOrDeleted(ent))
            return;

        _sprite.SetScale((ent, sprite), ent.Comp.OriginalScale);

        if (!TryComp<AnimationPlayerComponent>(ent, out var player) ||
            !_anim.HasRunningAnimation(player, FallAnimationKey))
        {
            return;
        }

        _anim.Stop((ent, player), FallAnimationKey);
    }

    private static Animation GetFallingAnimation(BinglePitFallingComponent component)
    {
        return new Animation
        {
            Length = component.AnimationTime,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(component.OriginalScale, 0.0f),
                        new AnimationTrackProperty.KeyFrame(component.AnimationScale, component.AnimationTime.Seconds),
                    },
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                },
            },
        };
    }
}
