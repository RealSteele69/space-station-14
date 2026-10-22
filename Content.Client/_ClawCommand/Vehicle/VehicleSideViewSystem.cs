using Content.Shared._ClawCommand.Vehicle;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - Shows the left- or right-facing state of a side-on vehicle sprite to match the way it points
/// on screen. Pointing mostly up or down keeps the last side, so it never looks like it's driving sideways.
/// </summary>
public sealed partial class VehicleSideViewSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>How far left or right it has to point (on screen) before it flips.</summary>
    private const float FlipThreshold = 0.3f;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eyeRotation = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<VehicleSideViewComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var side, out var sprite, out var xform))
        {
            var onScreen = (_transform.GetWorldRotation(xform) + eyeRotation).ToWorldVec();
            if (onScreen.X > FlipThreshold)
                side.FacingRight = true;
            else if (onScreen.X < -FlipThreshold)
                side.FacingRight = false;

            var state = side.State;
            if (_appearance.TryGetData<VehicleWearLevel>(uid, VehicleWearVisuals.Level, out var wear)
                && side.WearSuffixes.TryGetValue(wear, out var wearSuffix))
            {
                state += wearSuffix;
            }

            if (side.FacingRight)
                state += side.FlippedSuffix;

            if (state == side.ShownState)
                continue;

            side.ShownState = state;
            _sprite.LayerSetRsiState((uid, sprite), side.Layer, state);
        }
    }
}
