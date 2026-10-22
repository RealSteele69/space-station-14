// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Components;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Events;
using Content.Shared.Actions.Components;
using Robust.Shared.Map;

namespace Content.Server._ClawCommand.Lavaland.Megafauna.Systems;

public sealed partial class MegafaunaAttacksSystem
{
    private void InitializeProjectiles()
    {
        SubscribeLocalEvent<ActionsComponent, MegafaunaProjectileActionEvent>(OnProjectileAction);
    }

    private void OnProjectileAction(Entity<ActionsComponent> ent, ref MegafaunaProjectileActionEvent args)
    {
        if (args.Handled || args.Volleys <= 0 || args.Count <= 0)
            return;

        var comp = EnsureComp<MegafaunaProjectileBurstComponent>(ent);
        var burst = new MegafaunaProjectileBurst
        {
            Args = args,
            TargetEntity = args.Entity,
            TargetCoords = _xform.ToMapCoordinates(args.Target),
            VolleysLeft = args.Volleys,
            NextVolley = _timing.CurTime,
        };
        comp.Bursts.Add(burst);

        Shout(ent, args.Shout);

        // Fire the first volley right away so the attack reacts on the same tick it was chosen.
        FireVolley(ent, burst);
        args.Handled = true;
    }

    private void UpdateProjectiles(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<MegafaunaProjectileBurstComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_mobState.IsDead(uid))
            {
                RemCompDeferred<MegafaunaProjectileBurstComponent>(uid);
                continue;
            }

            for (var i = comp.Bursts.Count - 1; i >= 0; i--)
            {
                var burst = comp.Bursts[i];
                if (burst.VolleysLeft <= 0)
                {
                    comp.Bursts.RemoveAt(i);
                    continue;
                }

                if (burst.NextVolley > curTime)
                    continue;

                FireVolley(uid, burst);
            }

            if (comp.Bursts.Count == 0)
                RemCompDeferred<MegafaunaProjectileBurstComponent>(uid);
        }
    }

    private void FireVolley(EntityUid uid, MegafaunaProjectileBurst burst)
    {
        var args = burst.Args;
        burst.VolleysLeft--;
        burst.NextVolley = _timing.CurTime + TimeSpan.FromSeconds(args.VolleyDelay);

        var bossCoords = _xform.GetMapCoordinates(uid);
        if (bossCoords.MapId == MapId.Nullspace)
            return;

        if (args.TrackTarget
            && burst.TargetEntity is { } target
            && !TerminatingOrDeleted(target))
        {
            var targetCoords = _xform.GetMapCoordinates(target);
            if (targetCoords.MapId == bossCoords.MapId)
                burst.TargetCoords = targetCoords;
        }

        var aim = burst.TargetCoords.MapId == bossCoords.MapId
            ? burst.TargetCoords.Position - bossCoords.Position
            : Vector2.UnitY;

        if (aim.LengthSquared() < 0.0001f)
            aim = Vector2.UnitY;

        var baseAngle = Math.Atan2(aim.Y, aim.X)
                        + DegreesToRadians(args.AngleOffset + args.VolleyRotation * burst.VolleyIndex);
        burst.VolleyIndex++;

        for (var i = 0; i < args.Count; i++)
        {
            double angle;
            if (args.Radial)
                angle = baseAngle + Math.Tau * i / args.Count;
            else if (args.Count == 1)
                angle = baseAngle;
            else
                angle = baseAngle + DegreesToRadians(args.Spread) * ((double) i / (args.Count - 1) - 0.5);

            if (args.Jitter > 0f)
                angle += DegreesToRadians(_random.NextFloat(-args.Jitter, args.Jitter));

            var direction = new Vector2((float) Math.Cos(angle), (float) Math.Sin(angle));
            var projectile = Spawn(args.Projectile, bossCoords);
            _gun.ShootProjectile(projectile, direction, Vector2.Zero, uid, uid, args.ProjectileSpeed);
        }

        if (args.VolleySound != null)
            _audio.PlayPvs(args.VolleySound, uid);
    }

    private static double DegreesToRadians(float degrees)
        => degrees * Math.PI / 180.0;
}
