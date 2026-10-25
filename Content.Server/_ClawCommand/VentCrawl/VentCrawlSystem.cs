using System.Numerics;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._ClawCommand.VentCrawl;
using Content.Shared.Atmos;
using Content.Shared.Cuffs;
using Content.Shared.Eye;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NodeContainer;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.VentCrawl;

/// <summary>
/// Claw Command - vent crawling.
/// <list type="bullet">
/// <item>Alt-click an open vent or scrubber you are standing next to; after 4.5 seconds you are inside.</item>
/// <item>Movement keys follow the actual pipe connections: straight pipes, bends, manifolds, junctions, pumps,
/// every pipe layer, one segment at a time (relaymove / findConnecting).</item>
/// <item>Crawling into another vent or scrubber, or out of an open pipe end, climbs out there. Welded vents block.</item>
/// <item>People nearby hear you scrabbling through the ducts; the pipe's own air is what you breathe in there.</item>
/// </list>
/// </summary>
public sealed partial class VentCrawlSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EyeSystem _eye = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private WeldableSystem _weldable = default!;

    private const string DuctPrototype = "VentCrawlDuct";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasVentPumpComponent, GetVerbsEvent<AlternativeVerb>>(OnVentVerbs);
        SubscribeLocalEvent<GasVentScrubberComponent, GetVerbsEvent<AlternativeVerb>>(OnScrubberVerbs);
        SubscribeLocalEvent<VentCrawlerComponent, VentCrawlEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<VentCrawlingComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<VentCrawlingComponent, ComponentShutdown>(OnCrawlingShutdown);
        SubscribeLocalEvent<VentCrawlingComponent, AtmosExposedGetAirEvent>(OnCrawlerGetAir);
    }

    #region Entering

    /// <summary>ventcrawl_machinery: vent pumps and scrubbers are the doors in and out.</summary>
    public bool IsEntrance(EntityUid uid)
    {
        return HasComp<GasVentPumpComponent>(uid) || HasComp<GasVentScrubberComponent>(uid);
    }

    /// <summary>can_crawl_through(): welded vents and loose, unanchored fittings are shut.</summary>
    public bool CanCrawlThrough(EntityUid uid)
    {
        return Transform(uid).Anchored && !_weldable.IsWelded(uid);
    }

    private void OnVentVerbs(Entity<GasVentPumpComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        AddEnterVerb(ent, ref args);
    }

    private void OnScrubberVerbs(Entity<GasVentScrubberComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        AddEnterVerb(ent, ref args);
    }

    private void AddEnterVerb(EntityUid vent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !HasComp<VentCrawlerComponent>(args.User) || HasComp<VentCrawlingComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("vent-crawl-verb"),
            Message = Loc.GetString("vent-crawl-verb-desc"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/in.svg.192dpi.png")),
            Priority = 10,
            Act = () => TryStartEnter(user, vent),
        });
    }

    /// <summary>/mob/living/proc/handle_ventcrawl</summary>
    public bool TryStartEnter(EntityUid user, EntityUid vent)
    {
        if (!TryComp<VentCrawlerComponent>(user, out var crawler) || HasComp<VentCrawlingComponent>(user))
            return false;

        if (_mobState.IsIncapacitated(user) || IsRestrained(user))
        {
            _popup.PopupEntity(Loc.GetString("vent-crawl-cannot-now"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!CanCrawlThrough(vent))
        {
            _popup.PopupEntity(Loc.GetString("vent-crawl-welded"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!crawler.AllowHeldItems && !CheckCarry(user))
            return false;

        var node = GetPipeNode(vent);
        if (node?.NodeGroup == null || node.NodeGroup.Nodes.Count < 2)
        {
            _popup.PopupEntity(Loc.GetString("vent-crawl-not-connected"), user, user, PopupType.SmallCaution);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("vent-crawl-begin"), user, user);
        WarnAboutAir(user, node.Air);

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, crawler.EnterTime, new VentCrawlEnterDoAfterEvent(), user, vent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        });
    }

    /// <summary>incapacitated() counts restraints: cuffed mobs can't climb in.</summary>
    private bool IsRestrained(EntityUid user)
    {
        return _cuffable.IsCuffed(user, requireFullyCuffed: false);
    }

    /// <summary>
    /// ventcrawl_carry(): everything you carry counts, worn or held. A monkey or a pet with a hat on
    /// has to strip before it fits; only the body itself (and implants) goes in.
    /// </summary>
    private bool CheckCarry(EntityUid user)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(user))
        {
            // pulls and cuffs show up as virtual items in the hands
            if (HasComp<VirtualItemComponent>(item))
                continue;

            _popup.PopupEntity(Loc.GetString("vent-crawl-carrying", ("item", item)), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    /// <summary>The temperature and pressure hints you get as you start climbing in.</summary>
    private void WarnAboutAir(EntityUid user, GasMixture air)
    {
        var temperature = air.Temperature;
        string? tempMsg = temperature switch
        {
            < 260f => "vent-crawl-air-freezing",
            < Atmospherics.T0C => "vent-crawl-air-icy",
            > 360f => "vent-crawl-air-searing",
            > Atmospherics.T0C + 40 => "vent-crawl-air-hot",
            _ => null,
        };

        if (tempMsg != null)
            _popup.PopupEntity(Loc.GetString(tempMsg), user, user, PopupType.MediumCaution);

        var pressure = air.Pressure;
        string? pressureMsg = pressure switch
        {
            < Atmospherics.HazardLowPressure => "vent-crawl-pressure-rushing",
            < Atmospherics.WarningLowPressure => "vent-crawl-pressure-drag",
            > Atmospherics.HazardHighPressure => "vent-crawl-pressure-roaring",
            > Atmospherics.WarningHighPressure => "vent-crawl-pressure-current",
            _ => null,
        };

        if (pressureMsg != null)
            _popup.PopupEntity(Loc.GetString(pressureMsg), user, user, PopupType.MediumCaution);
    }

    private void OnEnterDoAfter(Entity<VentCrawlerComponent> ent, ref VentCrawlEnterDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } vent)
            return;

        args.Handled = true;
        if (_mobState.IsIncapacitated(ent) || IsRestrained(ent) || !CanCrawlThrough(vent) || (!ent.Comp.AllowHeldItems && !CheckCarry(ent)))
            return;

        Enter(ent, vent);
    }

    /// <summary>Puts a crawler straight into a vent or pipe, skipping the climb.</summary>
    public void Enter(EntityUid user, EntityUid pipe)
    {
        if (HasComp<VentCrawlingComponent>(user))
            return;

        if (TryComp<PullableComponent>(user, out var pullable) && pullable.Puller != null)
            _pulling.TryStopPull(user, pullable);
        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling is { } pulled && TryComp<PullableComponent>(pulled, out var pulledComp))
            _pulling.TryStopPull(pulled, pulledComp);

        _popup.PopupEntity(Loc.GetString("vent-crawl-enter", ("user", Identity.Entity(user, EntityManager))), user, Filter.PvsExcept(user), true, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("vent-crawl-enter-self"), user, user);

        var duct = Spawn(DuctPrototype, Transform(pipe).Coordinates);
        var ductComp = EnsureComp<VentCrawlDuctComponent>(duct);
        ductComp.Crawler = user;

        var container = _container.EnsureContainer<ContainerSlot>(duct, VentCrawlDuctComponent.ContainerId);
        if (!_container.Insert(user, container))
        {
            QueueDel(duct);
            return;
        }

        var crawling = EnsureComp<VentCrawlingComponent>(user);
        crawling.Pipe = pipe;
        crawling.Duct = duct;
        crawling.NextMove = _timing.CurTime + TimeSpan.FromSeconds(0.3);

        // You're in a duct, under the floor: walls and doors don't block your view the way they do upstairs.
        if (TryComp<EyeComponent>(user, out var eye))
        {
            crawling.PreviousDrawFov = eye.DrawFov;
            _eye.SetDrawFov(user, false, eye);
            // Pull every bit of piping over PVS, same as a t-ray would, without also networking the
            // cables/wires/disposal tubes hidden alongside it.
            _eye.SetVisibilityMask(user, eye.VisibilityMask | (int) VisibilityFlags.VentCrawlPipe, eye);
        }

        Dirty(user, crawling);
    }

    #endregion

    #region Moving

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<VentCrawlingComponent, VentCrawlerComponent>();
        while (query.MoveNext(out var uid, out var crawling, out var crawler))
        {
            // the pipe got cut or blown up around us
            if (crawling.Pipe is not { } pipe || TerminatingOrDeleted(pipe) || !Transform(pipe).Anchored)
            {
                Exit((uid, crawling), crawling.Duct is { } d && !TerminatingOrDeleted(d) ? Transform(d).Coordinates : Transform(uid).Coordinates, true);
                continue;
            }

            if (now < crawling.NextMove)
                continue;

            if (!TryComp<InputMoverComponent>(uid, out var mover) || GetInputDirection(mover) is not { } dir)
                continue;

            crawling.NextMove = now + TimeSpan.FromSeconds(GetMoveDelay(uid, crawler, mover));
            Step((uid, crawling, crawler), pipe, dir);
        }
    }

    /// <summary>Movement keys as a grid direction; diagonals are ignored.</summary>
    private static Direction? GetInputDirection(InputMoverComponent mover)
    {
        var buttons = mover.HeldMoveButtons;
        var x = ((buttons & MoveButtons.Right) != 0 ? 1 : 0) - ((buttons & MoveButtons.Left) != 0 ? 1 : 0);
        var y = ((buttons & MoveButtons.Up) != 0 ? 1 : 0) - ((buttons & MoveButtons.Down) != 0 ? 1 : 0);
        if (x == 0 && y == 0 || x != 0 && y != 0)
            return null;

        var vec = mover.RelativeRotation.RotateVec(new Vector2(x, y));
        return vec.ToWorldAngle().GetCardinalDir();
    }

    private float GetMoveDelay(EntityUid uid, VentCrawlerComponent crawler, InputMoverComponent mover)
    {
        var speed = 4f;
        if (TryComp<MovementSpeedModifierComponent>(uid, out var speedMod))
            speed = mover.Sprinting ? speedMod.CurrentSprintSpeed : speedMod.CurrentWalkSpeed;

        return Math.Clamp(1f / Math.Max(speed, 0.1f), crawler.MinMoveDelay, crawler.MaxMoveDelay);
    }

    /// <summary>
    /// relaymove / ventcrawl_to: follow a real connection into the next tile; crawling into an entrance climbs out of it;
    /// pushing against an open pipe end or out of a vent climbs out where you are.
    /// </summary>
    private void Step(Entity<VentCrawlingComponent, VentCrawlerComponent> ent, EntityUid pipe, Direction dir)
    {
        var pipeXform = Transform(pipe);
        if (pipeXform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var here = _map.TileIndicesFor(gridUid, grid, pipeXform.Coordinates);
        var there = here.Offset(dir);

        if (FindConnecting(pipe, gridUid, grid, there) is { } next)
        {
            if (IsEntrance(next))
            {
                if (!CanCrawlThrough(next))
                {
                    _popup.PopupEntity(Loc.GetString("vent-crawl-welded"), ent, ent, PopupType.SmallCaution);
                    return;
                }

                Exit(ent, Transform(next).Coordinates, false);
                return;
            }

            if (!CanCrawlThrough(next))
                return;

            ent.Comp1.Pipe = next;
            Dirty(ent, ent.Comp1);
            if (ent.Comp1.Duct is { } duct)
                _transform.SetCoordinates(duct, Transform(next).Coordinates);

            if (_timing.CurTime >= ent.Comp1.NextSound)
            {
                ent.Comp1.NextSound = _timing.CurTime + ent.Comp2.CrawlSoundCooldown;
                _audio.PlayPvs(ent.Comp2.CrawlSound, next);
            }

            return;
        }

        // nothing connects that way
        var openEnd = GetPipeNode(pipe) is { } node && (node.CurrentPipeDirection & dir.ToPipeDirection()) != 0;
        if (IsEntrance(pipe) && CanCrawlThrough(pipe) || openEnd)
        {
            if (_turf.GetTileRef(pipeXform.Coordinates) is { } tile && _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                return;

            Exit(ent, pipeXform.Coordinates, false);
        }
    }

    /// <summary>
    /// findConnecting(): the pipe-network node in <paramref name="target"/> that this pipe actually links to,
    /// on any of its layers.
    /// </summary>
    private EntityUid? FindConnecting(EntityUid pipe, EntityUid gridUid, MapGridComponent grid, Vector2i target)
    {
        if (!TryComp<NodeContainerComponent>(pipe, out var container))
            return null;

        foreach (var node in container.Nodes.Values)
        {
            if (node is not PipeNode)
                continue;

            foreach (var reachable in node.ReachableNodes)
            {
                if (reachable.Owner == pipe || reachable is not PipeNode || TerminatingOrDeleted(reachable.Owner))
                    continue;

                var otherXform = Transform(reachable.Owner);
                if (otherXform.GridUid != gridUid)
                    continue;

                if (_map.TileIndicesFor(gridUid, grid, otherXform.Coordinates) == target)
                    return reachable.Owner;
            }
        }

        return null;
    }

    private PipeNode? GetPipeNode(EntityUid uid)
    {
        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return null;

        foreach (var node in container.Nodes.Values)
        {
            if (node is PipeNode pipe)
                return pipe;
        }

        return null;
    }

    #endregion

    #region Leaving

    public void Exit(Entity<VentCrawlingComponent> ent, EntityCoordinates where, bool forced)
    {
        var duct = ent.Comp.Duct;
        ent.Comp.Duct = null;

        if (duct != null && _container.TryGetContainer(duct.Value, VentCrawlDuctComponent.ContainerId, out var container))
            _container.Remove(ent.Owner, container, destination: where, force: true);
        else
            _transform.SetCoordinates(ent, where);

        _transform.AttachToGridOrMap(ent);

        if (TryComp<EyeComponent>(ent, out var eye))
        {
            _eye.SetDrawFov(ent, ent.Comp.PreviousDrawFov, eye);
            _eye.SetVisibilityMask(ent, eye.VisibilityMask & ~(int) VisibilityFlags.VentCrawlPipe, eye);
        }

        RemComp<VentCrawlingComponent>(ent);
        if (duct != null)
            QueueDel(duct);

        _popup.PopupEntity(Loc.GetString(forced ? "vent-crawl-exit-forced" : "vent-crawl-exit-self"), ent, ent);
        _popup.PopupCoordinates(Loc.GetString("vent-crawl-exit"), where, Filter.PvsExcept(ent), true, PopupType.Medium);
    }

    private void OnMobStateChanged(Entity<VentCrawlingComponent> ent, ref MobStateChangedEvent args)
    {
        // a body stuck in the ducts helps nobody; it slides out where it stopped
        if (args.NewMobState == MobState.Dead && ent.Comp.Duct is { } duct)
            Exit(ent, Transform(duct).Coordinates, true);
    }

    private void OnCrawlingShutdown(Entity<VentCrawlingComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Duct is { } duct && !TerminatingOrDeleted(duct))
            QueueDel(duct);
    }

    #endregion

    // Inside the pipe you breathe (and feel the temperature of) the pipe's gas, not the room's.
    private void OnCrawlerGetAir(Entity<VentCrawlingComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled || ent.Comp.Pipe is not { } pipe)
            return;

        if (GetPipeNode(pipe) is not { } node)
            return;

        args.Gas = node.Air;
        args.Handled = true;
    }
}
