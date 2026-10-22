using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - the "biomorph" NPC xenomorphs:
/// line breaker and monarch charges, sprinter leaps, burrower tunnels, berserker rage, burster detonation,
/// heavy claws that send people flying, and their death screeches.
/// </summary>
public sealed partial class AdvXenoBiomorphSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    private const string TargetKey = "Target";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Structural = "Structural";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoBiomorphComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AdvXenoBiomorphComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<AdvXenoBiomorphComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AdvXenoBiomorphComponent, ThrowDoHitEvent>(OnThrowHit);
        SubscribeLocalEvent<AdvXenoBiomorphComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<AdvXenoBiomorphComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnMapInit(Entity<AdvXenoBiomorphComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpecial = _timing.CurTime + ent.Comp.SpecialCooldown;
    }

    private EntityUid? GetTarget(EntityUid uid)
    {
        if (!TryComp<HTNComponent>(uid, out var htn)
            || !htn.Blackboard.TryGetValue<EntityUid>(TargetKey, out var target, EntityManager)
            || TerminatingOrDeleted(target)
            || !HasComp<MobStateComponent>(target)
            || _mobState.IsDead(target))
        {
            return null;
        }

        return target;
    }

    #region Death

    private void OnMobStateChanged(Entity<AdvXenoBiomorphComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        _popup.PopupEntity(Loc.GetString(ent.Comp.DeathMessage, ("xeno", ent.Owner)), ent, PopupType.MediumCaution);
        _audio.PlayPvs(ent.Comp.DeathSound, ent);

        ent.Comp.ExecuteAt = null;
        ent.Comp.Charging = false;
        ent.Comp.BerserkUntil = null;
        _appearance.SetData(ent, AdvXenoBiomorphVisuals.Charging, false);
        _appearance.SetData(ent, AdvXenoBiomorphVisuals.Berserk, false);

        // /mob/living/simple_mob/animal/space/xenomorph/special/burster/proc/baneling
        if (ent.Comp.Special == AdvXenoBiomorphSpecial.Burst && !ent.Comp.Exploded && ent.Comp.ExplodeAt == null)
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-burster-rupture", ("xeno", ent.Owner)), ent, PopupType.LargeCaution);
            ent.Comp.ExplodeAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(1f, 2f));
            _appearance.SetData(ent, AdvXenoBiomorphVisuals.Berserk, true);
        }
    }

    #endregion

    #region Melee

    private void OnMeleeHit(Entity<AdvXenoBiomorphComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Weapon != ent.Owner)
            return;

        var berserk = ent.Comp.BerserkUntil is { } until && _timing.CurTime < until;
        foreach (var hit in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(hit))
                continue;

            // sprinter: apply_bonus_melee_damage x1.5 against the stunned
            if (ent.Comp.IncapacitatedDamageMultiplier > 1f && _mobState.IsIncapacitated(hit) || ent.Comp.IncapacitatedDamageMultiplier > 1f && HasComp<KnockedDownComponent>(hit))
                args.BonusDamage += args.BaseDamage * (ent.Comp.IncapacitatedDamageMultiplier - 1f);

            // berserk: outgoing_melee_damage_percent = 1.5
            if (berserk)
                args.BonusDamage += args.BaseDamage * 0.5f;

            // breaker / monarch apply_melee_effects: sends the victim flying
            if (ent.Comp.HeavyClaws && !HasComp<AdvXenoBiomorphComponent>(hit))
            {
                var direction = _transform.GetWorldPosition(hit) - _transform.GetWorldPosition(ent);
                if (direction.LengthSquared() < 0.01f)
                    continue;

                var distance = _mobState.IsIncapacitated(hit) || HasComp<KnockedDownComponent>(hit) ? 4f : 1f;
                _popup.PopupEntity(Loc.GetString("adv-xeno-heavy-claws", ("xeno", ent.Owner), ("target", hit)), hit, PopupType.MediumCaution);
                _audio.PlayPvs(ent.Comp.HeavyClawSound, hit);
                _throwing.TryThrow(hit, Vector2.Normalize(direction) * distance, 8f, ent, pushbackRatio: 0f, recoil: false);
            }
        }
    }

    #endregion

    #region Charge

    private void StartCharge(Entity<AdvXenoBiomorphComponent> ent, EntityUid target)
    {
        var from = _transform.GetWorldPosition(ent);
        var to = _transform.GetWorldPosition(target);
        var delta = to - from;
        if (delta.LengthSquared() < 0.01f)
            return;

        // charge two tiles past the target's turf
        var travel = delta + Vector2.Normalize(delta) * 2f;
        ent.Comp.Charging = true;
        _appearance.SetData(ent, AdvXenoBiomorphVisuals.Charging, true);
        _audio.PlayPvs(ent.Comp.ChargeSound, ent);
        if (!_throwing.TryThrow(ent, travel, 10f, ent, pushbackRatio: 0f, recoil: false, animated: false, playSound: false, doSpin: false))
            StopCharge(ent);
    }

    private void StopCharge(Entity<AdvXenoBiomorphComponent> ent)
    {
        if (!ent.Comp.Charging)
            return;

        ent.Comp.Charging = false;
        _appearance.SetData(ent, AdvXenoBiomorphVisuals.Charging, false);
    }

    // Bump() while charging: run people over, smash objects.
    private void OnThrowHit(Entity<AdvXenoBiomorphComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!ent.Comp.Charging)
            return;

        var hit = args.Target;
        _popup.PopupEntity(Loc.GetString("adv-xeno-charge-ram", ("xeno", ent.Owner), ("target", hit)), hit, PopupType.MediumCaution);

        if (HasComp<MobStateComponent>(hit))
        {
            _stun.TryUpdateParalyzeDuration(hit, ent.Comp.ChargeStun + ent.Comp.ChargeKnockdown);

            // runOver(): 2x to head and chest, 0.5x to each limb
            var damage = _random.Next(3, 5) * 6f;
            _damage.TryChangeDamage(hit, new DamageSpecifier(_proto.Index(Blunt), damage), origin: ent);
            _audio.PlayPvs(ent.Comp.ChargeHitSound, hit);

            // thrown to one side of the charge, turn(dir, +-45)
            var heading = _transform.GetWorldPosition(hit) - _transform.GetWorldPosition(ent);
            if (heading.LengthSquared() < 0.01f)
                heading = Vector2.UnitY;
            var side = _random.Prob(0.5f) ? 45 : -45;
            var dir = Angle.FromDegrees(side).RotateVec(Vector2.Normalize(heading));
            _throwing.TryThrow(hit, dir, 6f, ent, pushbackRatio: 0f, recoil: false);
        }
        else if (HasComp<DamageableComponent>(hit))
        {
            _damage.TryChangeDamage(hit, new DamageSpecifier(_proto.Index(Structural), ent.Comp.ChargeObjectDamage), origin: ent);
        }
    }

    private void OnLand(Entity<AdvXenoBiomorphComponent> ent, ref LandEvent args)
    {
        StopCharge(ent);

        if (ent.Comp.Special != AdvXenoBiomorphSpecial.Leap || ent.Comp.SpecialTarget is not { } target)
            return;

        // sprinter leap: knock down whoever we landed on
        ent.Comp.SpecialTarget = null;
        if (TerminatingOrDeleted(target) || (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(ent)).Length() > 1.2f)
            return;

        _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(4));
        _popup.PopupEntity(Loc.GetString("adv-xeno-sprinter-knockdown", ("xeno", ent.Owner), ("target", target)), target, PopupType.MediumCaution);
    }

    #endregion

    #region Berserk

    private void OnRefreshSpeed(Entity<AdvXenoBiomorphComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.BerserkUntil is { } until && _timing.CurTime < until)
            args.ModifySpeed(1.3f);
    }

    private void UpdateBerserk(Entity<AdvXenoBiomorphComponent> ent, TimeSpan now)
    {
        if (ent.Comp.BerserkUntil is { } until && now >= until)
        {
            ent.Comp.BerserkUntil = null;
            _appearance.SetData(ent, AdvXenoBiomorphVisuals.Berserk, false);
            _speed.RefreshMovementSpeedModifiers(ent.Owner);
            _popup.PopupEntity(Loc.GetString("adv-xeno-berserk-end", ("xeno", ent.Owner)), ent);
        }

        if (GetTarget(ent) == null
            || !HasComp<DamageableComponent>(ent)
            || !_thresholds.TryGetThresholdForState(ent, MobState.Dead, out var dead))
        {
            return;
        }

        // handle_special: at half health while fighting, rage (MODIFIER_STACK_EXTEND keeps refreshing it)
        if (_damage.GetTotalDamage(ent.Owner) < dead.Value / 2)
            return;

        var fresh = ent.Comp.BerserkUntil == null;
        ent.Comp.BerserkUntil = now + TimeSpan.FromSeconds(30);
        if (!fresh)
            return;

        _appearance.SetData(ent, AdvXenoBiomorphVisuals.Berserk, true);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
        _popup.PopupEntity(Loc.GetString("adv-xeno-berserk-start", ("xeno", ent.Owner)), ent, PopupType.LargeCaution);
    }

    #endregion

    #region Tunnel

    private bool TunnelPathClear(EntityUid uid, EntityUid target, out List<Vector2i> path)
    {
        path = new List<Vector2i>();
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;
        if (Transform(target).GridUid != gridUid)
            return false;

        var start = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var end = _map.TileIndicesFor(gridUid, grid, Transform(target).Coordinates);
        path = StepPath(start, end);
        foreach (var tile in path)
        {
            if (TileIsSolid(gridUid, grid, tile))
                return false;
        }

        return path.Count > 0;
    }

    private static List<Vector2i> StepPath(Vector2i start, Vector2i end)
    {
        var path = new List<Vector2i>();
        var cur = start;
        while (cur != end && path.Count < 32)
        {
            cur += new Vector2i(Math.Sign(end.X - cur.X), Math.Sign(end.Y - cur.Y));
            path.Add(cur);
        }

        return path;
    }

    private bool TileIsSolid(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), tile))
        {
            if (TryComp<PhysicsComponent>(ent, out var body) && body.Hard && body.CanCollide)
                return true;
        }

        return false;
    }

    private void TunnelStep(Entity<AdvXenoBiomorphComponent> ent, TimeSpan now)
    {
        var xform = Transform(ent);
        if (ent.Comp.TunnelPath.Count == 0 || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            Emerge(ent);
            return;
        }

        var next = ent.Comp.TunnelPath[0];
        ent.Comp.TunnelPath.RemoveAt(0);

        if (TileIsSolid(gridUid, grid, next))
        {
            // hit a wall: stunned and vulnerable
            _popup.PopupEntity(Loc.GetString("adv-xeno-burrower-solid", ("xeno", ent.Owner)), ent, PopupType.MediumCaution);
            _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(10));
            ent.Comp.TunnelPath.Clear();
            Emerge(ent);
            return;
        }

        var coords = _map.GridTileToLocal(gridUid, grid, next);

        _transform.SetCoordinates(ent, coords);
        var arrived = ent.Comp.TunnelPath.Count == 0;

        // stun anyone in our way, harder at the destination
        var hitAny = false;
        foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.45f))
        {
            if (other.Owner == ent.Owner)
                continue;
            _stun.TryUpdateParalyzeDuration(other, TimeSpan.FromSeconds(arrived ? 6 : 4));
            hitAny = true;
            if (arrived)
                _popup.PopupEntity(Loc.GetString("adv-xeno-burrower-erupt", ("xeno", ent.Owner), ("target", other.Owner)), other, PopupType.LargeCaution);
        }

        if (!arrived)
        {
            ent.Comp.NextTunnelStep = now + TimeSpan.FromSeconds(0.2);
            return;
        }

        if (!hitAny && !ent.Comp.Overshooting && ent.Comp.SpecialTarget is { } target && !TerminatingOrDeleted(target))
        {
            // overshoot 2-4 tiles past where they were
            ent.Comp.Overshooting = true;
            var start = _map.TileIndicesFor(gridUid, grid, coords);
            var targetTile = _map.TileIndicesFor(gridUid, grid, Transform(target).Coordinates);
            var dir = new Vector2i(Math.Sign(targetTile.X - start.X), Math.Sign(targetTile.Y - start.Y));
            if (dir == Vector2i.Zero)
                dir = new Vector2i(0, 1);
            var count = _random.Next(2, 5);
            for (var i = 1; i <= count; i++)
                ent.Comp.TunnelPath.Add(start + dir * i);
            ent.Comp.NextTunnelStep = now + TimeSpan.FromSeconds(0.2);
            return;
        }

        Emerge(ent);
    }

    private void Emerge(Entity<AdvXenoBiomorphComponent> ent)
    {
        ent.Comp.TunnelPath.Clear();
        ent.Comp.Overshooting = false;
        ent.Comp.SpecialTarget = null;
        _appearance.SetData(ent, AdvXenoBiomorphVisuals.Submerged, false);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<AdvXenoBiomorphComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Entity<AdvXenoBiomorphComponent> ent = (uid, comp);

            if (comp.ExplodeAt is { } explodeAt)
            {
                if (now >= explodeAt && !comp.Exploded)
                {
                    comp.Exploded = true;
                    comp.ExplodeAt = null;
                    _popup.PopupEntity(Loc.GetString("adv-xeno-burster-detonate", ("xeno", uid)), uid, PopupType.LargeCaution);
                    // explosion(1, 2, 4, 6)
                    _explosion.QueueExplosion(_transform.GetMapCoordinates(uid), ExplosionSystem.DefaultExplosionPrototypeId, 90f, 6f, 20f, uid);
                }
                continue;
            }

            if (_mobState.IsDead(uid))
                continue;

            if (comp.Special == AdvXenoBiomorphSpecial.Berserk)
            {
                if (now >= comp.NextSpecial)
                {
                    comp.NextSpecial = now + TimeSpan.FromSeconds(1);
                    UpdateBerserk(ent, now);
                }
                continue;
            }

            if (comp.Special == AdvXenoBiomorphSpecial.Tunnel && comp.TunnelPath.Count > 0 && comp.ExecuteAt == null)
            {
                if (now >= comp.NextTunnelStep)
                    TunnelStep(ent, now);
                continue;
            }

            if (comp.ExecuteAt is { } executeAt)
            {
                if (now < executeAt)
                    continue;
                comp.ExecuteAt = null;
                ExecuteSpecial(ent);
                continue;
            }

            if (comp.Special is AdvXenoBiomorphSpecial.None or AdvXenoBiomorphSpecial.Burst || now < comp.NextSpecial)
                continue;
            if (_mobState.IsIncapacitated(uid) || HasComp<KnockedDownComponent>(uid) || HasComp<StunnedComponent>(uid))
                continue;

            if (GetTarget(uid) is not { } target)
                continue;

            var distance = (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(uid)).Length();
            if (distance < comp.SpecialMinRange || distance > comp.SpecialMaxRange)
                continue;

            if (comp.Special == AdvXenoBiomorphSpecial.Tunnel && !TunnelPathClear(uid, target, out _))
                continue;

            // telegraph, then go
            comp.NextSpecial = now + comp.SpecialCooldown;
            comp.SpecialTarget = target;
            comp.ExecuteAt = now + comp.Warmup;
            var warn = comp.Special switch
            {
                AdvXenoBiomorphSpecial.Charge => "adv-xeno-charge-prepare",
                AdvXenoBiomorphSpecial.Leap => "adv-xeno-sprinter-prepare",
                _ => "adv-xeno-burrower-prepare",
            };
            _popup.PopupEntity(Loc.GetString(warn, ("xeno", uid), ("target", target)), uid, PopupType.MediumCaution);
        }
    }

    private void ExecuteSpecial(Entity<AdvXenoBiomorphComponent> ent)
    {
        if (ent.Comp.SpecialTarget is not { } target || TerminatingOrDeleted(target) || _mobState.IsIncapacitated(ent))
        {
            ent.Comp.SpecialTarget = null;
            return;
        }

        switch (ent.Comp.Special)
        {
            case AdvXenoBiomorphSpecial.Charge:
                ent.Comp.SpecialTarget = null;
                StartCharge(ent, target);
                break;

            case AdvXenoBiomorphSpecial.Leap:
            {
                var delta = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(ent);
                if (delta.LengthSquared() < 0.01f)
                    break;
                var landing = delta.Length() > 1f ? delta - Vector2.Normalize(delta) * 0.5f : delta;
                _popup.PopupEntity(Loc.GetString("adv-xeno-sprinter-leap", ("xeno", ent.Owner), ("target", target)), ent, PopupType.MediumCaution);
                _throwing.TryThrow(ent, landing, 10f, ent, pushbackRatio: 0f, recoil: false, animated: false, playSound: false, doSpin: false);
                break;
            }

            case AdvXenoBiomorphSpecial.Tunnel:
                if (!TunnelPathClear(ent, target, out var path))
                {
                    ent.Comp.SpecialTarget = null;
                    break;
                }

                _popup.PopupEntity(Loc.GetString("adv-xeno-burrower-tunnel", ("xeno", ent.Owner), ("target", target)), ent, PopupType.MediumCaution);
                _appearance.SetData(ent, AdvXenoBiomorphVisuals.Submerged, true);
                ent.Comp.TunnelPath = path;
                ent.Comp.Overshooting = false;
                ent.Comp.NextTunnelStep = _timing.CurTime + TimeSpan.FromSeconds(0.2);
                break;
        }
    }
}
