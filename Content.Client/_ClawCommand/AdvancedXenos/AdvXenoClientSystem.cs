using Content.Client.DamageState;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.Alert.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - client side of the advanced xenomorphs: the hive's view of infected hosts,
/// the plasma / growth counters, larva sprites and biomorph charge/burrow/berserk visuals.
/// </summary>
public sealed partial class AdvXenoClientSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoEmbryoComponent, GetStatusIconsEvent>(OnEmbryoIcons);
        SubscribeLocalEvent<AdvXenoPlasmaComponent, GetGenericAlertCounterAmountEvent>(OnPlasmaCounter);
        SubscribeLocalEvent<AdvXenoLarvaComponent, GetGenericAlertCounterAmountEvent>(OnGrowthCounter);
        SubscribeLocalEvent<AdvXenoBiomorphComponent, AppearanceChangeEvent>(OnBiomorphAppearance);
    }

    // AddInfectionImages(): "infected[stage]" over every host, seen only by the hive (factionIcon showTo).
    private void OnEmbryoIcons(Entity<AdvXenoEmbryoComponent> ent, ref GetStatusIconsEvent args)
    {
        var stage = Math.Clamp(ent.Comp.Stage, 0, 5);
        if (_proto.TryIndex<StatusIconPrototype>($"AdvXenoInfected{stage}", out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnPlasmaCounter(Entity<AdvXenoPlasmaComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled || args.Alert.ID != ent.Comp.Alert)
            return;

        args.Amount = ent.Comp.Plasma;
    }

    private void OnGrowthCounter(Entity<AdvXenoLarvaComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled || args.Alert.ID != ent.Comp.Alert)
            return;

        args.Amount = ent.Comp.Growth;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // /mob/living/carbon/alien/update_icons: larva[stage] plus _dead / _stun / _sleep
        var query = EntityQueryEnumerator<AdvXenoLarvaComponent, SpriteComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var appearance))
        {
            _appearance.TryGetData<int>(uid, AdvXenoLarvaVisuals.Stage, out var stage, appearance);
            var state = $"larva{Math.Clamp(stage, 0, 2)}";

            if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
                state += "_dead";
            else if (mob?.CurrentState == MobState.Critical || HasComp<StunnedComponent>(uid))
                state += "_stun";
            else if (HasComp<SleepingComponent>(uid) || HasComp<KnockedDownComponent>(uid))
                state += "_sleep";

            if (_sprite.LayerMapTryGet((uid, sprite), DamageStateVisualLayers.Base, out var layer, false))
                _sprite.LayerSetRsiState((uid, sprite), layer, state);

            _appearance.TryGetData<bool>(uid, AdvXenoLarvaVisuals.Hidden, out var hidden, appearance);
            _sprite.SetDrawDepth((uid, sprite), (int) (hidden ? Content.Shared.DrawDepth.DrawDepth.SmallMobs : Content.Shared.DrawDepth.DrawDepth.Mobs));
        }
    }

    private void OnBiomorphAppearance(Entity<AdvXenoBiomorphComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        var spriteEnt = (ent.Owner, sprite);

        if (_appearance.TryGetData<bool>(ent, AdvXenoBiomorphVisuals.Submerged, out var submerged, args.Component))
            _sprite.SetVisible(spriteEnt, !submerged);

        var berserk = _appearance.TryGetData<bool>(ent, AdvXenoBiomorphVisuals.Berserk, out var b, args.Component) && b;
        _sprite.SetColor(spriteEnt, berserk ? Color.FromHex("#FF8080") : Color.White);

        if (ent.Comp.ChargeState == null || ent.Comp.AliveState == null)
            return;

        if (!_sprite.LayerMapTryGet(spriteEnt, DamageStateVisualLayers.Base, out var layer, false))
            return;

        var charging = _appearance.TryGetData<bool>(ent, AdvXenoBiomorphVisuals.Charging, out var c, args.Component) && c;
        if (charging)
            _sprite.LayerSetRsiState(spriteEnt, layer, ent.Comp.ChargeState);
        else if (!TryComp<MobStateComponent>(ent, out var mob) || mob.CurrentState == MobState.Alive)
            _sprite.LayerSetRsiState(spriteEnt, layer, ent.Comp.AliveState);
    }
}
