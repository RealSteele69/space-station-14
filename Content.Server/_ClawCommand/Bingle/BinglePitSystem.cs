// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Shared._ClawCommand.Bingle;
using Content.Shared.ActionBlocker;
using Content.Shared.Destructible;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Humanoid;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.StatusEffectNew;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Bingle;

public sealed partial class BinglePitSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private BingleSystem _bingle = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedScaleVisualsSystem _scale = default!;

    private static readonly EntProtoId BingleMob = "MobBingle";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BinglePitComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<BinglePitComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<BinglePitComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BinglePitComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<BinglePitComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<BinglePitFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<BinglePitFallingComponent, ComponentShutdown>(OnFallingShutdown);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BinglePitFallingComponent>();
        while (query.MoveNext(out var uid, out var falling))
        {
            if (_timing.CurTime < falling.NextDeletionTime)
                continue;

            RemCompDeferred(uid, falling);

            if (!TryComp<BinglePitComponent>(falling.Pit, out var pit) || pit.Pit == null)
                continue;

            _container.Insert(uid, pit.Pit);
            // Keeps anything swallowed from acting inside the pit until it is destroyed.
            _stun.TryUpdateStunDuration(uid, null);
        }
    }

    private void OnMapInit(Entity<BinglePitComponent> ent, ref MapInitEvent args)
    {
        if (!Transform(ent).Coordinates.IsValid(EntityManager))
            QueueDel(ent);

        ent.Comp.Pit = _container.EnsureContainer<Container>(ent, "pit");
    }

    private void OnStepTriggered(Entity<BinglePitComponent> ent, ref StepTriggeredOffEvent args)
    {
        var tripper = args.Tripper;

        if (HasComp<BinglePitFallingComponent>(tripper))
            return;

        // Needs to be at level two or above to swallow anything alive.
        if (HasComp<MobStateComponent>(tripper) && ent.Comp.Level < 2)
            return;

        // Only dead bingles fall in.
        if (HasComp<BingleComponent>(tripper)
            && TryComp<MobStateComponent>(tripper, out var mobState)
            && mobState.CurrentState == MobState.Alive)
            return;

        StartFalling(ent, tripper);

        var cost = BingleCost(ent.Comp);
        if (ent.Comp.BinglePoints >= cost && CountBingles(ent) < ent.Comp.MaxBingles)
        {
            SpawnBingle(ent);
            ent.Comp.BinglePoints -= cost;
        }
    }

    private static float BingleCost(BinglePitComponent comp)
    {
        return comp.SpawnNewAt * comp.Level;
    }

    private void StartFalling(Entity<BinglePitComponent> ent, EntityUid tripper)
    {
        var comp = ent.Comp;

        if (TryComp<MobStateComponent>(tripper, out var mobState) && mobState.CurrentState is MobState.Alive or MobState.Critical)
            comp.BinglePoints += comp.PointsForAlive;
        else
            comp.BinglePoints++;

        if (HasComp<HumanoidProfileComponent>(tripper))
            comp.BinglePoints += BingleCost(comp) * comp.HumanoidBingleFraction;

        // Recycling a bingle returns a quarter of one.
        if (HasComp<BingleComponent>(tripper))
            comp.BinglePoints += BingleCost(comp) / 4;

        if (TryComp<PullableComponent>(tripper, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(tripper, pullable);

        var fall = EnsureComp<BinglePitFallingComponent>(tripper);
        fall.Pit = ent;
        fall.NextDeletionTime = _timing.CurTime + fall.DeletionTime;
        _blocker.UpdateCanMove(tripper);
        _stun.TryKnockdown(tripper, fall.DeletionTime, refresh: false);

        _audio.PlayPvs(comp.FallingSound, ent);
    }

    private void OnStepTriggerAttempt(Entity<BinglePitComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }

    /// <summary>
    /// Living bingles tied to this pit plus unclaimed bingle ghost roles sitting on it.
    /// </summary>
    private int CountBingles(Entity<BinglePitComponent> ent)
    {
        var count = 0;

        var bingles = EntityQueryEnumerator<BingleComponent, MobStateComponent>();
        while (bingles.MoveNext(out _, out var bingle, out var mobState))
        {
            if (bingle.MyPit == ent.Owner && mobState.CurrentState != MobState.Dead)
                count++;
        }

        var pitCoords = Transform(ent).Coordinates;
        var spawners = EntityQueryEnumerator<GhostRoleMobSpawnerComponent, TransformComponent>();
        while (spawners.MoveNext(out _, out var spawner, out var xform))
        {
            if (spawner.Prototype == BingleMob && xform.Coordinates == pitCoords)
                count++;
        }

        return count;
    }

    public void SpawnBingle(Entity<BinglePitComponent> ent)
    {
        Spawn(ent.Comp.GhostRoleToSpawn, Transform(ent).Coordinates);
        SpreadFloor(ent, ent.Comp.Level * 2);

        ent.Comp.MinionsMade++;
        if (ent.Comp.MinionsMade < ent.Comp.UpgradeMinionsAfter)
            return;

        ent.Comp.MinionsMade = 0;
        ent.Comp.Level++;
        UpgradeBingles(ent);
    }

    public void UpgradeBingles(Entity<BinglePitComponent> ent)
    {
        var query = EntityQueryEnumerator<BingleComponent>();
        while (query.MoveNext(out var uid, out var bingle))
        {
            if (bingle.MyPit == ent.Owner)
                _bingle.UpgradeBingle((uid, bingle));
        }

        if (ent.Comp.Level <= ent.Comp.MaxSize)
            _scale.SetSpriteScale(ent, Vector2.One * ent.Comp.Level);

        _popup.PopupEntity(Loc.GetString("bingle-pit-grow"), ent);
    }

    private void OnDestruction(Entity<BinglePitComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.Pit != null)
        {
            foreach (var swallowed in _container.EmptyContainer(ent.Comp.Pit))
            {
                _status.TryRemoveStatusEffect(swallowed, SharedStunSystem.StunId);
                _stun.TryKnockdown(swallowed, TimeSpan.FromSeconds(2), refresh: false);
            }
        }

        RemoveAllBingleGhostRoles(ent);

        // In case someone is between starting to fall and being inserted.
        var query = EntityQueryEnumerator<BinglePitFallingComponent>();
        while (query.MoveNext(out var uid, out var falling))
        {
            if (falling.Pit == ent.Owner)
                RemCompDeferred(uid, falling);
        }
    }

    /// <summary>
    /// Removes the unclaimed bingle ghost roles sitting on a pit.
    /// </summary>
    public void RemoveAllBingleGhostRoles(Entity<BinglePitComponent> ent)
    {
        var pitCoords = Transform(ent).Coordinates;
        var query = EntityQueryEnumerator<GhostRoleMobSpawnerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawner, out var xform))
        {
            if (spawner.Prototype == BingleMob && xform.Coordinates == pitCoords)
                QueueDel(uid);
        }
    }

    private void OnAttacked(Entity<BinglePitComponent> ent, ref AttackedEvent args)
    {
        if (_container.ContainsEntity(ent, args.User))
            _stun.TryUpdateStunDuration(args.User, null);
    }

    private void OnUpdateCanMove(Entity<BinglePitFallingComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnFallingShutdown(Entity<BinglePitFallingComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _blocker.UpdateCanMove(ent);
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        var query = AllEntityQuery<BinglePitComponent, TransformComponent>();
        while (query.MoveNext(out _, out var comp, out var xform))
        {
            var location = "Unknown";
            var mapCoords = _transform.ToMapCoordinates(xform.Coordinates);
            if (_navMap.TryGetNearestBeacon(mapCoords, out var beacon, out _) && beacon.Value.Comp.Text is { } text)
                location = text;

            var points = comp.BinglePoints + comp.MinionsMade * comp.SpawnNewAt * comp.Level;

            ev.AddLine(Loc.GetString("bingle-pit-end-of-round",
                ("location", location),
                ("level", comp.Level),
                ("points", points)));
        }
    }

    /// <summary>
    /// Turns some of the floor around the pit into bingle floor.
    /// </summary>
    private void SpreadFloor(EntityUid uid, float radius)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return;

        var convertTile = (ContentTileDefinition) _tileDef[Comp<BinglePitComponent>(uid).FloorTile];
        var center = xform.Coordinates.Position;
        var box = new Box2(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var tiles = _map.GetLocalTilesIntersecting(gridUid, grid, box);

        while (tiles.MoveNext(out var tile))
        {
            if (tile.Tile.TypeId == convertTile.TileId)
                continue;

            var current = _turf.GetContentTileDefinition(tile);
            if (!current.Sturdy || !_random.Prob(0.1f))
                continue;

            _tile.ReplaceTile(tile, convertTile);
        }
    }
}
