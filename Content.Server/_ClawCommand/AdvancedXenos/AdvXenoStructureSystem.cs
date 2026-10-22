using Content.Server.Atmos.Components;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - alien structures: weeds spreading from nodes, eggs growing and bursting,
/// and corrosive acid eating through whatever it was vomited on.
/// </summary>
public sealed partial class AdvXenoStructureSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AdvXenoFacehuggerSystem _hugger = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly SoundSpecifier AcidSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");

    private static readonly ProtoId<DamageTypePrototype> Structural = "Structural";

    private static readonly Vector2i[] Cardinals = { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoWeedNodeComponent, MapInitEvent>(OnNodeInit);
        SubscribeLocalEvent<AdvXenoEggComponent, MapInitEvent>(OnEggInit);
        SubscribeLocalEvent<AdvXenoEggComponent, InteractHandEvent>(OnEggInteractHand);
        SubscribeLocalEvent<AdvXenoEggComponent, DamageChangedEvent>(OnEggDamaged);
        SubscribeLocalEvent<AdvXenoAcidComponent, ComponentShutdown>(OnAcidShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var nodes = EntityQueryEnumerator<AdvXenoWeedNodeComponent, TransformComponent>();
        while (nodes.MoveNext(out var uid, out var node, out var xform))
        {
            if (now < node.NextSpread)
                continue;
            node.NextSpread = now + node.SpreadInterval;
            SpreadWeeds((uid, node, xform));
        }

        var eggs = EntityQueryEnumerator<AdvXenoEggComponent>();
        while (eggs.MoveNext(out var uid, out var egg))
        {
            UpdateEgg((uid, egg), now);
        }

        var acids = EntityQueryEnumerator<AdvXenoAcidComponent>();
        while (acids.MoveNext(out var uid, out var acid))
        {
            if (now >= acid.NextTick)
                TickAcid((uid, acid), now);
        }
    }

    #region Weeds

    private void OnNodeInit(Entity<AdvXenoWeedNodeComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpread = _timing.CurTime + ent.Comp.SpreadInterval;
    }

    /// <summary>
    /// /obj/structure/alien/weeds/process for a node and every weed within its range: each weed
    /// tries to grow into the four cardinal tiles that are floor, not walled off and not already weedy.
    /// </summary>
    private void SpreadWeeds(Entity<AdvXenoWeedNodeComponent, TransformComponent> node)
    {
        if (!node.Comp2.Anchored || node.Comp2.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _map.TileIndicesFor(gridUid, grid, node.Comp2.Coordinates);
        var range = node.Comp1.Range;

        var weedTiles = new List<Vector2i>();
        for (var dx = -range; dx <= range; dx++)
        {
            for (var dy = -range; dy <= range; dy++)
            {
                var tile = origin + new Vector2i(dx, dy);
                if (GetWeeds(gridUid, grid, tile) is { } weed)
                {
                    weedTiles.Add(tile);
                    // unlinked weeds in range get adopted by this node
                    if (TryComp<AdvXenoWeedsComponent>(weed, out var weedComp) && (weedComp.Node == null || Deleted(weedComp.Node)))
                        weedComp.Node = node.Owner;
                }
            }
        }

        // prob(max(10, 40 - (5 * nearby_weeds.len))) for every weed but the node itself
        var chance = Math.Max(10, 40 - 5 * weedTiles.Count) / 100f;
        foreach (var from in weedTiles)
        {
            if (from != origin && !_random.Prob(chance))
                continue;

            foreach (var dir in Cardinals)
            {
                var to = from + dir;
                if (GetWeeds(gridUid, grid, to) != null || !CanWeedsGrow(gridUid, grid, to))
                    continue;

                var weed = Spawn(node.Comp1.Weeds, _map.GridTileToLocal(gridUid, grid, to));
                if (TryComp<AdvXenoWeedsComponent>(weed, out var weedComp))
                    weedComp.Node = node.Owner;
            }
        }
    }

    private EntityUid? GetWeeds(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), tile))
        {
            if (HasComp<AdvXenoWeedsComponent>(ent))
                return ent;
        }

        return null;
    }

    private bool CanWeedsGrow(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || _turf.IsSpace(tileRef))
            return false;

        foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), tile))
        {
            // T.density / CheckAirBlock == ATMOS_PASS_AIR_BLOCKED
            if (TryComp<AirtightComponent>(ent, out var airtight) && airtight.AirBlocked)
                return false;
        }

        return true;
    }

    #endregion

    #region Eggs

    private void OnEggInit(Entity<AdvXenoEggComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Status == AdvXenoEggStatus.Growing)
        {
            var span = ent.Comp.MaxGrowTime - ent.Comp.MinGrowTime;
            ent.Comp.GrowAt = _timing.CurTime + ent.Comp.MinGrowTime + span * _random.NextDouble();
        }

        UpdateEggVisuals(ent);
    }

    private void UpdateEgg(Entity<AdvXenoEggComponent> ent, TimeSpan now)
    {
        switch (ent.Comp.Status)
        {
            case AdvXenoEggStatus.Growing:
                if (ent.Comp.GrowAt is { } growAt && now >= growAt)
                {
                    ent.Comp.Status = AdvXenoEggStatus.Grown;
                    ent.Comp.GrowAt = null;
                    Dirty(ent);
                    UpdateEggVisuals(ent);
                }
                break;

            case AdvXenoEggStatus.Grown:
                if (now < ent.Comp.NextScan)
                    break;
                ent.Comp.NextScan = now + TimeSpan.FromSeconds(1);

                // Proximity()/process(): a grown egg opens for anything a hugger could take,
                // except a host that is already carrying and still awake.
                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, ent.Comp.TriggerRange))
                {
                    if (!_hugger.CanHug(mob))
                        continue;
                    if (HasComp<AdvXenoEmbryoComponent>(mob) && _mobState.IsAlive(mob))
                        continue;

                    Burst(ent, false);
                    break;
                }
                break;

            case AdvXenoEggStatus.Bursting:
                if (ent.Comp.BurstDoneAt is { } doneAt && now >= doneAt)
                    FinishBurst(ent);
                break;
        }
    }

    // /obj/structure/alien/egg/proc/Burst
    public bool Burst(Entity<AdvXenoEggComponent> ent, bool kill)
    {
        if (ent.Comp.Status is not (AdvXenoEggStatus.Grown or AdvXenoEggStatus.Growing))
            return false;

        // A growing egg has no child yet; breaking it just leaves the shell.
        if (ent.Comp.Status == AdvXenoEggStatus.Growing)
            kill = true;

        ent.Comp.Status = AdvXenoEggStatus.Bursting;
        ent.Comp.KillOnBurst = kill;
        ent.Comp.BurstDoneAt = _timing.CurTime + TimeSpan.FromSeconds(1.5);
        Dirty(ent);
        UpdateEggVisuals(ent);
        return true;
    }

    private void FinishBurst(Entity<AdvXenoEggComponent> ent)
    {
        ent.Comp.Status = AdvXenoEggStatus.Burst;
        ent.Comp.BurstDoneAt = null;
        Dirty(ent);
        UpdateEggVisuals(ent);

        var coords = Transform(ent).Coordinates;
        var child = Spawn(ent.Comp.Facehugger, coords);
        if (ent.Comp.KillOnBurst)
        {
            _hugger.Die(child);
            return;
        }

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 1.5f))
        {
            if (_hugger.CanHug(mob) && _hugger.TryAttach(child, mob))
                break;
        }
    }

    // /obj/structure/alien/egg/attack_hand: the hive tends its eggs.
    private void OnEggInteractHand(Entity<AdvXenoEggComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !HasComp<AdvXenoHiveComponent>(args.User))
            return;

        args.Handled = true;
        switch (ent.Comp.Status)
        {
            case AdvXenoEggStatus.Burst:
                _popup.PopupEntity(Loc.GetString("adv-xeno-egg-clear"), ent, args.User);
                QueueDel(ent);
                break;
            case AdvXenoEggStatus.Growing:
                _popup.PopupEntity(Loc.GetString("adv-xeno-egg-growing"), ent, args.User);
                break;
            case AdvXenoEggStatus.Grown:
                _popup.PopupEntity(Loc.GetString("adv-xeno-egg-retrieve"), ent, args.User);
                Burst(ent, false);
                break;
        }
    }

    // integrity_failure: the shell cracks and the child inside dies.
    private void OnEggDamaged(Entity<AdvXenoEggComponent> ent, ref DamageChangedEvent args)
    {
        if (_damage.GetTotalDamage(ent.Owner) >= ent.Comp.BreakDamage)
            Burst(ent, true);
    }

    private void UpdateEggVisuals(Entity<AdvXenoEggComponent> ent)
    {
        _appearance.SetData(ent, AdvXenoEggVisuals.Status, ent.Comp.Status);
    }

    #endregion

    #region Acid

    private void OnAcidShutdown(Entity<AdvXenoAcidComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Target is { } target && !TerminatingOrDeleted(target))
            RemCompDeferred<AdvXenoAcidedComponent>(target);
    }

    // /obj/structure/alien/acid/proc/tick
    private void TickAcid(Entity<AdvXenoAcidComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Target is not { } target || TerminatingOrDeleted(target))
        {
            QueueDel(ent);
            return;
        }

        ent.Comp.Ticks++;
        ent.Comp.NextTick = now + TimeSpan.FromSeconds(_random.NextFloat(15f, 20f));

        // keep the goop sitting on its target if it got moved
        var targetCoords = Transform(target).Coordinates;
        _transform.SetCoordinates(ent, targetCoords);

        if (ent.Comp.Ticks >= ent.Comp.Strength)
        {
            _popup.PopupCoordinates(Loc.GetString("adv-xeno-acid-collapse", ("target", target)), targetCoords, PopupType.LargeCaution);
            _audio.PlayPvs(AcidSound, targetCoords);
            ent.Comp.Target = null;
            // dismantle_wall()/qdel(): overwhelming structural damage runs the target's own destruction
            // behaviours (girders, sheets, ...); anything that shrugs it off is simply deleted.
            _damage.TryChangeDamage(target, new DamageSpecifier(_proto.Index(Structural), 100000), ignoreResistances: true);
            if (!TerminatingOrDeleted(target))
                QueueDel(target);
            QueueDel(ent);
            return;
        }

        var remaining = ent.Comp.Strength - ent.Comp.Ticks;
        var msg = remaining switch
        {
            6 => "adv-xeno-acid-holding",
            4 => "adv-xeno-acid-melting",
            2 => "adv-xeno-acid-struggling",
            <= 1 => "adv-xeno-acid-crumbling",
            _ => null,
        };

        if (msg != null)
            _popup.PopupCoordinates(Loc.GetString(msg, ("target", target)), targetCoords, PopupType.MediumCaution);
    }

    #endregion
}
