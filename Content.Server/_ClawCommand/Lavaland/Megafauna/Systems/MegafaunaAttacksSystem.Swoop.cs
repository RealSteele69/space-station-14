// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Components;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Events;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;

namespace Content.Server._ClawCommand.Lavaland.Megafauna.Systems;

public sealed partial class MegafaunaAttacksSystem
{
    private readonly HashSet<Entity<MobStateComponent>> _landHits = new();

    private void InitializeSwoop()
    {
        SubscribeLocalEvent<ActionsComponent, MegafaunaSwoopActionEvent>(OnSwoopAction);
        SubscribeLocalEvent<MegafaunaAirborneComponent, BeforeDamageChangedEvent>(OnAirborneDamage);
        SubscribeLocalEvent<MegafaunaAirborneComponent, ComponentShutdown>(OnAirborneShutdown);
    }

    private void OnSwoopAction(Entity<ActionsComponent> ent, ref MegafaunaSwoopActionEvent args)
    {
        if (args.Handled
            || HasComp<MegafaunaAirborneComponent>(ent)
            || HasComp<MegafaunaChargingComponent>(ent))
            return;

        var curTime = _timing.CurTime;
        var airborne = AddComp<MegafaunaAirborneComponent>(ent);
        airborne.Args = args;
        airborne.TargetEntity = args.Entity;
        airborne.TrackEnd = curTime + TimeSpan.FromSeconds(args.TrackTime);
        airborne.LandTime = airborne.TrackEnd + TimeSpan.FromSeconds(args.LockTime);
        airborne.WasAwake = PauseNpc(ent);
        airborne.Shadow = Spawn(args.Shadow, _xform.ToMapCoordinates(args.Target));

        _appearance.SetData(ent, MegafaunaVisuals.Airborne, true);
        _physics.SetCanCollide(ent, false);

        if (args.TakeoffSound != null)
            _audio.PlayPvs(args.TakeoffSound, ent);

        Shout(ent, args.Shout);
        args.Handled = true;
    }

    private void UpdateSwoop(TimeSpan curTime, float frameTime)
    {
        var query = EntityQueryEnumerator<MegafaunaAirborneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var airborne, out var xform))
        {
            if (_mobState.IsDead(uid)
                || airborne.Shadow is not { } shadow
                || TerminatingOrDeleted(shadow))
            {
                RemCompDeferred<MegafaunaAirborneComponent>(uid);
                continue;
            }

            if (curTime < airborne.TrackEnd)
            {
                MoveShadow(airborne, shadow, frameTime);
                continue;
            }

            if (curTime < airborne.LandTime)
                continue;

            Land(uid, airborne, xform, shadow);
        }
    }

    private void MoveShadow(MegafaunaAirborneComponent airborne, EntityUid shadow, float frameTime)
    {
        if (airborne.TargetEntity is not { } target || TerminatingOrDeleted(target))
            return;

        var shadowCoords = _xform.GetMapCoordinates(shadow);
        var targetCoords = _xform.GetMapCoordinates(target);
        if (shadowCoords.MapId != targetCoords.MapId)
            return;

        var delta = targetCoords.Position - shadowCoords.Position;
        var maxStep = airborne.Args.ShadowSpeed * frameTime;
        var step = delta.Length() <= maxStep ? delta : Vector2.Normalize(delta) * maxStep;
        _xform.SetWorldPosition(shadow, shadowCoords.Position + step);
    }

    private void Land(EntityUid uid, MegafaunaAirborneComponent airborne, TransformComponent xform, EntityUid shadow)
    {
        var args = airborne.Args;
        var landCoords = _xform.GetMapCoordinates(shadow);
        QueueDel(shadow);
        airborne.Shadow = null;

        if (landCoords.MapId == xform.MapID)
            _xform.SetWorldPosition((uid, xform), landCoords.Position);

        RemCompDeferred<MegafaunaAirborneComponent>(uid);

        if (args.LandSound != null)
            _audio.PlayPvs(args.LandSound, uid);

        if (args.LandSpawn is { } spawn)
            Spawn(spawn, landCoords);

        _landHits.Clear();
        _lookup.GetEntitiesInRange(landCoords, args.LandRadius, _landHits);
        foreach (var target in _landHits)
        {
            if (target.Owner == uid)
                continue;

            if (args.LandDamage != null)
                _damage.TryChangeDamage(target.Owner, args.LandDamage, origin: uid);

            if (args.KnockbackDistance > 0f)
            {
                var away = _xform.GetWorldPosition(target.Owner) - landCoords.Position;
                if (away.LengthSquared() < 0.01f)
                    away = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));
                if (away.LengthSquared() > 0.0001f)
                    _throwing.TryThrow(target.Owner, Vector2.Normalize(away) * args.KnockbackDistance, 10f, uid);
            }

            if (args.KnockdownTime > 0f)
                _stun.TryKnockdown(target.Owner, TimeSpan.FromSeconds(args.KnockdownTime));
        }
    }

    private void OnAirborneDamage(Entity<MegafaunaAirborneComponent> ent, ref BeforeDamageChangedEvent args)
    {
        // Can't hit what's high up in the sky.
        args.Cancelled = true;
    }

    private void OnAirborneShutdown(Entity<MegafaunaAirborneComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Shadow is { } shadow && !TerminatingOrDeleted(shadow))
            QueueDel(shadow);

        if (TerminatingOrDeleted(ent))
            return;

        _appearance.SetData(ent, MegafaunaVisuals.Airborne, false);
        _physics.SetCanCollide(ent, true);
        ResumeNpc(ent, ent.Comp.WasAwake);
    }
}
