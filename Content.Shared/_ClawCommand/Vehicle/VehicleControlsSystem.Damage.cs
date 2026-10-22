using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Repairable;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - Vehicle upkeep: maintenance panel + welder repairs, shootable hulls, EMP stalls,
/// emag "safeties off", and visible wear.
/// </summary>
public sealed partial class VehicleControlsSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly EntProtoId SparkEffect = "EffectSparks";

    private static readonly TimeSpan WearCheckInterval = TimeSpan.FromSeconds(0.5);

    private const int HullShotLayers = (int) (CollisionGroup.BulletImpassable | CollisionGroup.Opaque);

    private void InitializeDamage()
    {
        SubscribeLocalEvent<VehicleControlsComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(RepairableSystem) });
        SubscribeLocalEvent<VehicleControlsComponent, VehiclePanelDoAfterEvent>(OnPanelDoAfter);
        SubscribeLocalEvent<VehicleControlsComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<VehicleControlsComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<VehicleControlsComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<VehicleControlsComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<VehicleControlsComponent, VehicleCanRunEvent>(OnCanRun,
            after: new[] { typeof(VehicleSystem) });
        SubscribeLocalEvent<VehicleOccupantComponent, ShotAttemptedEvent>(OnOccupantShotAttempt);
    }

    /// <summary>
    /// Hulls stop bullets (BulletImpassable) and lasers (Opaque). RequireProjectileTarget on the
    /// prototype means they still have to be aimed at, so shots at people nearby carry on.
    /// </summary>
    private void MakeHullShootable(Entity<VehicleControlsComponent> ent)
    {
        if (!ent.Comp.Shootable || !TryComp<FixturesComponent>(ent, out var fixtures))
            return;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (fixture.Hard && (fixture.CollisionLayer & HullShotLayers) != HullShotLayers)
                _physics.SetCollisionLayer(ent, id, fixture, fixture.CollisionLayer | HullShotLayers, fixtures);
        }
    }

    /// <summary>
    /// The driver, anyone buckled on, and anyone riding inside.
    /// </summary>
    public bool IsAboard(EntityUid vehicle, EntityUid other)
    {
        if (_vehicle.GetOperatorOrNull(vehicle) == other)
            return true;

        if (TryComp<BuckleComponent>(other, out var buckle) && buckle.BuckledTo == vehicle)
            return true;

        return _vehicle.TryGetOperatorContainer(vehicle, out var container) && container.Contains(other);
    }

    #region Maintenance panel + repairs

    private void OnInteractUsing(Entity<VehicleControlsComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.RequirePanelForRepair)
            return;

        if (_tool.HasQuality(args.Used, ScrewingQuality.Id))
        {
            args.Handled = _tool.UseTool(args.Used, args.User, ent.Owner, ent.Comp.PanelDelay, ScrewingQuality,
                new VehiclePanelDoAfterEvent());
            return;
        }

        // Welding a closed vehicle does nothing; tell them why instead of letting the repair start.
        if (!ent.Comp.PanelOpen
            && HasComp<RepairableComponent>(ent)
            && _tool.HasQuality(args.Used, WeldingQuality.Id))
        {
            args.Handled = true;
            _popup.PopupClient(Loc.GetString("vehicle-panel-closed-repair", ("vehicle", ent.Owner)), ent.Owner, args.User);
        }
    }

    private void OnPanelDoAfter(Entity<VehicleControlsComponent> ent, ref VehiclePanelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.PanelOpen = !ent.Comp.PanelOpen;
        Dirty(ent);

        var msg = ent.Comp.PanelOpen ? "vehicle-panel-opened" : "vehicle-panel-closed";
        _popup.PopupClient(Loc.GetString(msg, ("vehicle", ent.Owner)), ent.Owner, args.User);
    }

    private void OnExamined(Entity<VehicleControlsComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        switch (ent.Comp.Wear)
        {
            case VehicleWearLevel.Damaged:
                args.PushMarkup(Loc.GetString("vehicle-examine-damaged"));
                break;
            case VehicleWearLevel.Critical:
                args.PushMarkup(Loc.GetString("vehicle-examine-critical"));
                break;
        }

        if (ent.Comp.RequirePanelForRepair && HasComp<RepairableComponent>(ent))
            args.PushMarkup(Loc.GetString(ent.Comp.PanelOpen ? "vehicle-examine-panel-open" : "vehicle-examine-panel-closed"));

        if (_emag.CheckFlag(ent, EmagType.Interaction))
            args.PushMarkup(Loc.GetString("vehicle-examine-emagged"));

        ExamineIgnition(ent, ref args);
    }

    #endregion

    #region Shots

    private void OnPreventCollide(Entity<VehicleControlsComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !TryComp<ProjectileComponent>(args.OtherEntity, out var projectile))
            return;

        // Turret gunners and riders never shoot their own vehicle.
        if (projectile.Shooter is { } shooter && IsAboard(ent, shooter))
        {
            args.Cancelled = true;
            return;
        }

        // Open vehicles: most shots at the vehicle carry on into whoever is riding it.
        // Decided per projectile so repeat contact checks can't re-roll it.
        if (ent.Comp.RiderHitChance > 0f
            && TryComp<StrapComponent>(ent, out var strap)
            && strap.BuckledEntities.Count > 0
            && (uint) (args.OtherEntity.Id * 2654435761u) % 1000u < (uint) (ent.Comp.RiderHitChance * 1000f))
        {
            args.Cancelled = true;
        }
    }

    /// <summary>
    /// From inside an enclosed vehicle you can work a turret, but not shoot your own guns.
    /// </summary>
    private void OnOccupantShotAttempt(Entity<VehicleOccupantComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!HasComp<VehicleTurretWeaponComponent>(args.Used))
            args.Cancel();
    }

    #endregion

    #region EMP + emag

    private void OnEmpPulse(Entity<VehicleControlsComponent> ent, ref EmpPulseEvent args)
    {
        // Upstream adds EmpDisabledComponent for the duration; the engine won't run while it's there.
        args.Affected = true;
        args.Disabled = true;
        _popup.PopupEntity(Loc.GetString("vehicle-emp-stall", ("vehicle", ent.Owner)), ent.Owner);
    }

    private void OnEmagged(Entity<VehicleControlsComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || _emag.CheckFlag(ent, EmagType.Interaction))
            return;

        args.Handled = true;
        _vehicle.RefreshCanRun(ent.Owner);
    }

    private void OnCanRun(Entity<VehicleControlsComponent> ent, ref VehicleCanRunEvent args)
    {
        if (HasComp<EmpDisabledComponent>(ent))
        {
            args.CanRun = false;
            return;
        }

        // Hotwired: an emagged vehicle runs without its key.
        if (!args.CanRun && _emag.CheckFlag(ent, EmagType.Interaction))
            args.CanRun = true;
    }

    #endregion

    #region Wear (server)

    private void UpdateWear(TimeSpan now)
    {
        var query = EntityQueryEnumerator<VehicleControlsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.NextWearCheck)
            {
                comp.NextWearCheck = now + WearCheckInterval;

                var total = (float) _damageable.GetTotalDamage(uid);
                var wear = total >= comp.CriticalThreshold ? VehicleWearLevel.Critical
                    : total >= comp.DamagedThreshold ? VehicleWearLevel.Damaged
                    : VehicleWearLevel.Fine;

                if (wear != comp.Wear)
                {
                    if (wear > comp.Wear)
                    {
                        var msg = wear == VehicleWearLevel.Critical ? "vehicle-wear-critical" : "vehicle-wear-damaged";
                        _popup.PopupEntity(Loc.GetString(msg, ("vehicle", uid)), uid, PopupType.SmallCaution);
                    }

                    comp.Wear = wear;
                    Dirty(uid, comp);
                    _appearance.SetData(uid, VehicleWearVisuals.Level, wear);
                }

                // EMP stalls end on their own; let the engine catch again.
                var stalled = HasComp<EmpDisabledComponent>(uid);
                if (stalled != comp.Stalled)
                {
                    comp.Stalled = stalled;
                    _vehicle.RefreshCanRun(uid);
                }
            }

            if (comp.Wear == VehicleWearLevel.Critical && now >= comp.NextSpark)
            {
                comp.NextSpark = now + TimeSpan.FromSeconds(_random.NextFloat(1.5f, 4f));
                Spawn(SparkEffect, Transform(uid).Coordinates);
            }
        }
    }

    #endregion
}
