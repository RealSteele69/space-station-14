// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Components;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Events;
using Content.Shared._ClawCommand.Lavaland.Mobs;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Server._ClawCommand.Lavaland.Megafauna.Systems;

public sealed partial class MegafaunaAttacksSystem
{
    /// <summary>
    /// How far from a wall the boss stops so its body doesn't end up inside it.
    /// </summary>
    private const float ChargeWallMargin = 1.1f;

    private readonly HashSet<Entity<MobStateComponent>> _chargeHits = new();

    private void InitializeCharge()
    {
        SubscribeLocalEvent<ActionsComponent, MegafaunaChargeActionEvent>(OnChargeAction);
        SubscribeLocalEvent<MegafaunaChargingComponent, ComponentShutdown>(OnChargeShutdown);
    }

    private void OnChargeAction(Entity<ActionsComponent> ent, ref MegafaunaChargeActionEvent args)
    {
        if (args.Handled
            || HasComp<MegafaunaChargingComponent>(ent)
            || HasComp<MegafaunaAirborneComponent>(ent))
            return;

        var charge = AddComp<MegafaunaChargingComponent>(ent);
        charge.Args = args;
        charge.TargetEntity = args.Entity;
        charge.TargetCoords = _xform.ToMapCoordinates(args.Target);
        charge.ChargesLeft = Math.Max(1, args.Charges);
        charge.WasAwake = PauseNpc(ent);

        StartWindUp(ent, charge, args.WindUp);
        Shout(ent, args.Shout);
        args.Handled = true;
    }

    private void StartWindUp(EntityUid uid, MegafaunaChargingComponent charge, float windUp)
    {
        charge.DashStart = _timing.CurTime + TimeSpan.FromSeconds(windUp);
        charge.Hit.Clear();
        charge.LastTrailTile = null;

        _appearance.SetData(uid, MegafaunaVisuals.Charging, true);

        if (charge.Args.WindUpSound != null)
            _audio.PlayPvs(charge.Args.WindUpSound, uid);

        if (charge.Args.WindUpSpawn is { } spawn)
            Spawn(spawn, _xform.GetMapCoordinates(uid));
    }

    private void UpdateCharge(TimeSpan curTime, float frameTime)
    {
        var query = EntityQueryEnumerator<MegafaunaChargingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var charge, out var xform))
        {
            if (_mobState.IsDead(uid) || xform.MapID == MapId.Nullspace)
            {
                RemCompDeferred<MegafaunaChargingComponent>(uid);
                continue;
            }

            if (charge.DashStart is { } dashStart)
            {
                if (dashStart > curTime)
                    continue;

                BeginDash(uid, charge, xform);
                continue;
            }

            StepDash(uid, charge, xform, frameTime);
        }
    }

    private void BeginDash(EntityUid uid, MegafaunaChargingComponent charge, TransformComponent xform)
    {
        charge.DashStart = null;

        var origin = _xform.GetWorldPosition(xform);
        if (charge.TargetEntity is { } target && !TerminatingOrDeleted(target))
        {
            var targetCoords = _xform.GetMapCoordinates(target);
            if (targetCoords.MapId == xform.MapID)
                charge.TargetCoords = targetCoords;
        }

        var delta = charge.TargetCoords.MapId == xform.MapID
            ? charge.TargetCoords.Position - origin
            : Vector2.Zero;

        if (delta.LengthSquared() < 0.01f)
        {
            FinishCharge(uid, charge);
            return;
        }

        var distance = Math.Min(delta.Length() + charge.Args.Overshoot, charge.Args.MaxDistance);
        var direction = Vector2.Normalize(delta);

        // Stop in front of the first wall in the way instead of phasing through it.
        var ray = new CollisionRay(origin, direction, (int) CollisionGroup.Impassable);
        foreach (var hit in _physics.IntersectRay(xform.MapID, ray, distance, uid, returnOnFirstHit: false))
        {
            if (HasComp<MobStateComponent>(hit.HitEntity))
                continue;

            distance = Math.Min(distance, hit.Distance - ChargeWallMargin);
            break;
        }

        if (distance <= 0.25f)
        {
            NextChargeOrFinish(uid, charge);
            return;
        }

        charge.Direction = direction;
        charge.DistanceLeft = distance;
    }

    private void StepDash(EntityUid uid, MegafaunaChargingComponent charge, TransformComponent xform, float frameTime)
    {
        var step = Math.Min(charge.Args.ChargeSpeed * frameTime, charge.DistanceLeft);
        var position = _xform.GetWorldPosition(xform) + charge.Direction * step;
        _xform.SetWorldPosition((uid, xform), position);
        charge.DistanceLeft -= step;

        var mapCoords = new MapCoordinates(position, xform.MapID);
        SpawnTrail(charge, mapCoords);
        HitAlongCharge(uid, charge, mapCoords);

        if (charge.DistanceLeft <= 0.001f)
            NextChargeOrFinish(uid, charge);
    }

    private void SpawnTrail(MegafaunaChargingComponent charge, MapCoordinates coords)
    {
        if (charge.Args.TrailSpawn is not { } trail)
            return;

        var tile = new Vector2i((int) Math.Floor(coords.Position.X), (int) Math.Floor(coords.Position.Y));
        if (charge.LastTrailTile == tile)
            return;

        charge.LastTrailTile = tile;
        Spawn(trail, coords);
    }

    private void HitAlongCharge(EntityUid uid, MegafaunaChargingComponent charge, MapCoordinates coords)
    {
        _chargeHits.Clear();
        _lookup.GetEntitiesInRange(coords, charge.Args.HitRadius, _chargeHits);

        foreach (var target in _chargeHits)
        {
            if (target.Owner == uid
                || !charge.Hit.Add(target.Owner)
                || !charge.Args.HitFauna && HasComp<FaunaComponent>(target.Owner))
                continue;

            if (charge.Args.Damage != null)
                _damage.TryChangeDamage(target.Owner, charge.Args.Damage, origin: uid);

            if (charge.Args.KnockbackDistance > 0f)
            {
                // Shove targets away from the charge line, biased forward, so the boss plows through them.
                var away = _xform.GetWorldPosition(target.Owner) - coords.Position;
                var side = away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.Zero;
                var pushDir = charge.Direction + side;
                if (pushDir.LengthSquared() < 0.01f)
                    pushDir = charge.Direction;
                var push = Vector2.Normalize(pushDir) * charge.Args.KnockbackDistance;
                _throwing.TryThrow(target.Owner, push, charge.Args.KnockbackSpeed, uid);
            }

            if (charge.Args.KnockdownTime > 0f)
                _stun.TryKnockdown(target.Owner, TimeSpan.FromSeconds(charge.Args.KnockdownTime));

            if (charge.Args.HitSound != null)
                _audio.PlayPvs(charge.Args.HitSound, target.Owner);
        }
    }

    private void NextChargeOrFinish(EntityUid uid, MegafaunaChargingComponent charge)
    {
        charge.ChargesLeft--;
        if (charge.ChargesLeft <= 0)
        {
            FinishCharge(uid, charge);
            return;
        }

        StartWindUp(uid, charge, charge.Args.ChainWindUp);
    }

    private void FinishCharge(EntityUid uid, MegafaunaChargingComponent charge)
    {
        RemCompDeferred<MegafaunaChargingComponent>(uid);
    }

    private void OnChargeShutdown(Entity<MegafaunaChargingComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _appearance.SetData(ent, MegafaunaVisuals.Charging, false);
        ResumeNpc(ent, ent.Comp.WasAwake);
    }
}
