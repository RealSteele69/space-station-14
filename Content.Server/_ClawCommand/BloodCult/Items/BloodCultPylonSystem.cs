using System.Linq;
using System.Numerics;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared._ClawCommand.BloodCult.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.BloodCult.Items;

/// <summary>
/// Pylons heal nearby cult members and slowly turn the floor around them into cult floor.
/// </summary>
public sealed partial class BloodCultPylonSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BloodCultItemSystem _cultItem = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodCultPylonComponent, InteractHandEvent>(OnInteractHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BloodCultPylonComponent>();
        while (query.MoveNext(out var uid, out var pylon))
        {
            if (!pylon.IsActive)
                continue;

            if (now >= pylon.NextCorruption)
            {
                pylon.NextCorruption = now + pylon.CorruptionCooldown;
                CorruptTileInRange((uid, pylon));
            }

            if (now >= pylon.NextHealing)
            {
                pylon.NextHealing = now + pylon.HealingCooldown;
                HealInRange((uid, pylon));
            }
        }
    }

    private void OnInteractHand(Entity<BloodCultPylonComponent> pylon, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_cultItem.IsCultMember(args.User))
        {
            _audio.PlayPvs(pylon.Comp.BurnHandSound, pylon);
            _popup.PopupEntity(Loc.GetString("cult-pylon-burn-hand"), pylon, args.User);
            _damageable.TryChangeDamage(args.User, pylon.Comp.DamageOnInteract, true);
            return;
        }

        pylon.Comp.IsActive = !pylon.Comp.IsActive;
        _appearance.SetData(pylon, PylonVisuals.Activated, pylon.Comp.IsActive);
        _pointLight.SetEnabled(pylon, pylon.Comp.IsActive);
        _popup.PopupEntity(Loc.GetString(pylon.Comp.IsActive ? "pylon-toggle-on" : "pylon-toggle-off"), pylon);
    }

    private void CorruptTileInRange(Entity<BloodCultPylonComponent> pylon)
    {
        var xform = Transform(pylon);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !_tileDefinition.TryGetDefinition(pylon.Comp.CultTile, out var def) || def is not ContentTileDefinition cultTile)
            return;

        var radius = pylon.Comp.CorruptionRadius;
        var pos = xform.Coordinates.Position;
        var tiles = _map.GetLocalTilesIntersecting(gridUid, grid, new Box2(pos - new Vector2(radius, radius), pos + new Vector2(radius, radius)))
            .ToList();

        _random.Shuffle(tiles);

        foreach (var tile in tiles)
        {
            if (tile.Tile.TypeId == cultTile.TileId || _turf.IsSpace(tile))
                continue;

            var center = _turf.GetTileCenter(tile);
            _audio.PlayPvs(pylon.Comp.CorruptTileSound, center, AudioParams.Default.WithVolume(-5));
            _tile.ReplaceTile(tile, cultTile);
            Spawn(pylon.Comp.TileCorruptEffect, center);
            return;
        }
    }

    private void HealInRange(Entity<BloodCultPylonComponent> pylon)
    {
        var coords = Transform(pylon).Coordinates;
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, pylon.Comp.HealingAuraRange))
        {
            if (!_cultItem.IsCultMember(target) || HasComp<Content.Shared.Ghost.Components.GhostComponent>(target) || _mobState.IsDead(target))
                continue;

            foreach (var (group, amount) in pylon.Comp.Healing)
            {
                _damageable.HealEvenly(target.Owner, amount, group);
            }
        }
    }
}
