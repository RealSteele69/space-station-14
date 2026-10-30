using Content.Client.VendingMachines;
using Content.Shared._ClawCommand.Lavaland.Salvage.Vendor;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._ClawCommand.Lavaland.Salvage.Vendor;

/// <summary>
/// Client-side visuals for <see cref="ShopVendorComponent"/>, mirroring vanilla's client
/// VendingMachineSystem (which computes visual state from networked component fields rather
/// than the appearance system).
/// </summary>
public sealed partial class ShopVendorSystem : SharedShopVendorSystem
{
    [Dependency] private AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShopVendorComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<ShopVendorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ShopVendorComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<ShopVendorComponent, ComponentStartup>(OnStartup);
    }

    private void OnHandleState(Entity<ShopVendorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        TryUpdateVisualState(ent);
    }

    private void OnPowerChanged(Entity<ShopVendorComponent> ent, ref PowerChangedEvent args)
    {
        TryUpdateVisualState(ent);
    }

    private void OnAnimationCompleted(Entity<ShopVendorComponent> ent, ref AnimationCompletedEvent args)
    {
        TryUpdateVisualState(ent);
    }

    private void OnStartup(Entity<ShopVendorComponent> ent, ref ComponentStartup args)
    {
        TryUpdateVisualState(ent);
    }

    private void TryUpdateVisualState(Entity<ShopVendorComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var state = GetVisualState(ent);
        UpdateAppearance(ent, state, sprite);
    }

    private VendingMachineVisualState GetVisualState(Entity<ShopVendorComponent> ent)
    {
        if (ent.Comp.Broken)
            return VendingMachineVisualState.Broken;

        if (ent.Comp.Ejecting != null)
            return VendingMachineVisualState.Eject;

        if (ent.Comp.Denying)
            return VendingMachineVisualState.Deny;

        if (!_power.IsPowered(ent.Owner))
            return VendingMachineVisualState.Off;

        return VendingMachineVisualState.Normal;
    }

    private void UpdateAppearance(Entity<ShopVendorComponent> ent, VendingMachineVisualState state, SpriteComponent sprite)
    {
        var comp = ent.Comp;
        SetLayerState(VendingMachineVisualLayers.Base, comp.OffState, (ent.Owner, sprite));

        switch (state)
        {
            case VendingMachineVisualState.Normal:
                SetLayerState(VendingMachineVisualLayers.BaseUnshaded, comp.NormalState, (ent.Owner, sprite));
                SetLayerState(VendingMachineVisualLayers.Screen, comp.ScreenState, (ent.Owner, sprite));
                break;

            case VendingMachineVisualState.Deny:
                if (comp.LoopDenyAnimation)
                    SetLayerState(VendingMachineVisualLayers.BaseUnshaded, comp.DenyState, (ent.Owner, sprite));
                else
                    PlayAnimation(ent.Owner, VendingMachineVisualLayers.BaseUnshaded, comp.DenyState, comp.DenyDelay, sprite);

                SetLayerState(VendingMachineVisualLayers.Screen, comp.ScreenState, (ent.Owner, sprite));
                break;

            case VendingMachineVisualState.Eject:
                PlayAnimation(ent.Owner, VendingMachineVisualLayers.BaseUnshaded, comp.EjectState, comp.EjectDelay, sprite);
                SetLayerState(VendingMachineVisualLayers.Screen, comp.ScreenState, (ent.Owner, sprite));
                break;

            case VendingMachineVisualState.Broken:
                HideLayers((ent.Owner, sprite));
                SetLayerState(VendingMachineVisualLayers.Base, comp.BrokenState, (ent.Owner, sprite));
                break;

            case VendingMachineVisualState.Off:
                HideLayers((ent.Owner, sprite));
                break;
        }
    }

    private void SetLayerState(VendingMachineVisualLayers layer, string? state, Entity<SpriteComponent> sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
        _sprite.LayerSetAutoAnimated(sprite.AsNullable(), layer, true);
        _sprite.LayerSetRsiState(sprite.AsNullable(), layer, state);
    }

    private void PlayAnimation(EntityUid uid, VendingMachineVisualLayers layer, string? state, TimeSpan time, SpriteComponent sprite)
    {
        if (string.IsNullOrEmpty(state) || _animationPlayer.HasRunningAnimation(uid, state))
            return;

        var animation = GetAnimation(layer, state, time);
        _sprite.LayerSetVisible((uid, sprite), layer, true);
        _animationPlayer.Play(uid, animation, state);
    }

    private static Animation GetAnimation(VendingMachineVisualLayers layer, string state, TimeSpan time)
    {
        return new Animation
        {
            Length = time,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = layer,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(state, 0f)
                    }
                }
            }
        };
    }

    private void HideLayers(Entity<SpriteComponent> sprite)
    {
        HideLayer(VendingMachineVisualLayers.BaseUnshaded, sprite);
        HideLayer(VendingMachineVisualLayers.Screen, sprite);
    }

    private void HideLayer(VendingMachineVisualLayers layer, Entity<SpriteComponent> sprite)
    {
        if (!_sprite.LayerMapTryGet(sprite.AsNullable(), layer, out var actualLayer, false))
            return;

        _sprite.LayerSetVisible(sprite.AsNullable(), actualLayer, false);
    }
}
