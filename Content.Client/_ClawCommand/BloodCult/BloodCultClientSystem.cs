using System.Numerics;
using Content.Client.Light.Components;
using Content.Client.Light.EntitySystems;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared._ClawCommand.BloodCult.Systems;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client._ClawCommand.BloodCult;

/// <summary>
/// Cult status icons and the halos the cult shows once it grows large.
/// </summary>
public sealed partial class BloodCultClientSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultHaloComponent, ComponentStartup>(OnHaloAdded);
        SubscribeLocalEvent<BloodCultHaloComponent, ComponentShutdown>(OnHaloRemoved);

        SubscribeLocalEvent<BloodCultistComponent, GetStatusIconsEvent>(OnCultistIcons);
        SubscribeLocalEvent<BloodCultLeaderComponent, GetStatusIconsEvent>(OnLeaderIcons);
        SubscribeLocalEvent<BloodCultConstructComponent, GetStatusIconsEvent>(OnConstructIcons);
    }

    private void OnHaloAdded(Entity<BloodCultHaloComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || _sprite.LayerMapTryGet((ent, sprite), BloodCultHaloKey.Key, out _, false))
            return;

        var state = _random.Pick(ent.Comp.States);
        var layer = _sprite.AddLayer((ent, sprite), new SpriteSpecifier.Rsi(new ResPath(ent.Comp.RsiPath), state));
        _sprite.LayerMapSet((ent, sprite), BloodCultHaloKey.Key, layer);
        sprite.LayerSetShader(layer, "unshaded");

        var bounds = _sprite.GetLocalBounds((ent, sprite));
        _sprite.LayerSetOffset((ent, sprite), layer, new Vector2(0f, bounds.Height / 2f + 10f / 32f));
    }

    private void OnHaloRemoved(Entity<BloodCultHaloComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !_sprite.LayerMapTryGet((ent, sprite), BloodCultHaloKey.Key, out var layer, false))
            return;

        _sprite.RemoveLayer((ent, sprite), layer);
    }

    private void OnCultistIcons(Entity<BloodCultistComponent> ent, ref GetStatusIconsEvent args)
    {
        // The leader shows the leader icon instead.
        if (HasComp<BloodCultLeaderComponent>(ent))
            return;

        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnLeaderIcons(Entity<BloodCultLeaderComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnConstructIcons(Entity<BloodCultConstructComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!ent.Comp.Holy && _prototype.TryIndex(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }
}

public sealed partial class BloodCultPhaseShiftSystem : SharedBloodCultPhaseShiftSystem;

/// <summary>
/// Lights or snuffs the void torch as its charges run out.
/// </summary>
public sealed partial class VoidTorchVisualizerSystem : VisualizerSystem<VoidTorchVisualsComponent>
{
    [Dependency] private LightBehaviorSystem _lightBehavior = default!;

    protected override void OnAppearanceChange(EntityUid uid, VoidTorchVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (!AppearanceSystem.TryGetData<bool>(uid, GenericCultVisuals.State, out var lit, args.Component) ||
            !TryComp<LightBehaviourComponent>(uid, out var behaviour))
            return;

        _lightBehavior.StopLightBehaviour((uid, behaviour));
        _lightBehavior.StartLightBehaviour((uid, behaviour), lit ? component.TurnOnBehaviour : component.TurnOffBehaviour);
    }
}

[RegisterComponent]
public sealed partial class VoidTorchVisualsComponent : Component
{
    [DataField]
    public string TurnOnBehaviour = "turn_on";

    [DataField]
    public string TurnOffBehaviour = "fade_out";
}
