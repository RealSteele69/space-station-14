using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - Grants and handles the actions from <see cref="VehicleControlsComponent"/>.
/// </summary>
public sealed partial class VehicleControlsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleSystem _vehicle = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedVerbSystem _verbs = default!;

    private static readonly TimeSpan IgnitionHintCooldown = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleControlsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleControlsComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<VehicleControlsComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<VehicleControlsComponent, EntInsertedIntoContainerMessage>(OnOccupantInserted);
        SubscribeLocalEvent<VehicleControlsComponent, EntRemovedFromContainerMessage>(OnOccupantRemoved);
        SubscribeLocalEvent<VehicleControlsComponent, ContainerVehicleEntryAttemptEvent>(OnEntryAttempt);
        SubscribeLocalEvent<VehicleControlsComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<VehicleControlsComponent, InteractHandEvent>(OnInteractHand);

        SubscribeLocalEvent<VehicleControlsComponent, VehicleHornActionEvent>(OnHorn);
        SubscribeLocalEvent<VehicleControlsComponent, VehicleSirenActionEvent>(OnSiren);
        SubscribeLocalEvent<VehicleControlsComponent, VehicleExitActionEvent>(OnExit);
        SubscribeLocalEvent<VehicleControlsComponent, VehicleManTurretActionEvent>(OnManTurret);

        InitializeDamage();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsServer)
            UpdateWear(_timing.CurTime);
    }

    private void OnMapInit(Entity<VehicleControlsComponent> ent, ref MapInitEvent args)
    {
        UpdateSiren(ent);
        MakeHullShootable(ent);
        _appearance.SetData(ent.Owner, VehicleWearVisuals.Level, ent.Comp.Wear);
    }

    private void OnOperatorSet(Entity<VehicleControlsComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (args.OldOperator != null)
        {
            _actions.RemoveAction(ent.Comp.HornActionEntity);
            _actions.RemoveAction(ent.Comp.SirenActionEntity);
        }

        if (args.NewOperator is { } driver)
        {
            // Whoever takes the wheel lets go of any turret they were manning.
            ReleaseTurret(ent, driver);

            if (ent.Comp.HornAction is { } horn)
                _actions.AddAction(driver, ref ent.Comp.HornActionEntity, horn, ent.Owner);

            if (ent.Comp.SirenAction is { } siren
                && _actions.AddAction(driver, ref ent.Comp.SirenActionEntity, siren, ent.Owner))
            {
                _actions.SetToggled(ent.Comp.SirenActionEntity, ent.Comp.SirenOn);
            }

            Dirty(ent);
        }
        else if (args.OldOperator is { } oldDriver)
        {
            TryPromotePassenger(ent, oldDriver);
        }
    }

    /// <summary>
    /// When the driver leaves (or can no longer drive), the first passenger able to drive takes the wheel.
    /// </summary>
    private void TryPromotePassenger(EntityUid vehicle, EntityUid oldDriver)
    {
        if (_vehicle.HasOperator(vehicle)
            || !TryComp<VehicleComponent>(vehicle, out var vehicleComp)
            || !_vehicle.TryGetOperatorContainer(vehicle, out var container))
            return;

        foreach (var occupant in container.ContainedEntities)
        {
            if (occupant != oldDriver && _vehicle.TrySetOperator((vehicle, vehicleComp), occupant))
                return;
        }
    }

    /// <summary>
    /// Only mobs ride inside. Anything an occupant drops falls out under the vehicle instead of
    /// landing in the occupant container (hands drop into the container their holder is in).
    /// </summary>
    private void OnInsertAttempt(Entity<VehicleControlsComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled
            || !_vehicle.TryGetOperatorContainer(ent.Owner, out var container)
            || args.Container != container)
            return;

        if (!HasComp<MobStateComponent>(args.EntityUid))
            args.Cancel();
    }

    private void OnOccupantInserted(Entity<VehicleControlsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_timing.ApplyingState
            || !_vehicle.TryGetOperatorContainer(ent.Owner, out var container)
            || args.Container != container)
            return;

        if (_net.IsServer)
            EnsureComp<VehicleOccupantComponent>(args.Entity).Vehicle = ent.Owner;

        EntityUid? action = null;
        if (ent.Comp.ExitAction is { } exit && _actions.AddAction(args.Entity, ref action, exit, ent.Owner))
            ent.Comp.ExitActionEntities[args.Entity] = action.Value;

        action = null;
        if (ent.Comp.TurretWeapons.Count > 0 && _actions.AddAction(args.Entity, ref action, ent.Comp.TurretAction, ent.Owner))
            ent.Comp.TurretActionEntities[args.Entity] = action.Value;
    }

    private void OnOccupantRemoved(Entity<VehicleControlsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        ReleaseTurret(ent, args.Entity);

        if (_net.IsServer)
            RemComp<VehicleOccupantComponent>(args.Entity);

        if (ent.Comp.ExitActionEntities.Remove(args.Entity, out var exit))
        {
            _actions.RemoveAction(exit);
            QueueDel(exit);
        }

        if (ent.Comp.TurretActionEntities.Remove(args.Entity, out var turret))
        {
            _actions.RemoveAction(turret);
            QueueDel(turret);
        }
    }

    private void OnEntryAttempt(Entity<VehicleControlsComponent> ent, ref ContainerVehicleEntryAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.MaxOccupants is not { } max)
            return;

        if (_vehicle.TryGetOperatorContainer(ent.Owner, out var container) && container.ContainedEntities.Count >= max)
            args.Cancelled = true;
    }

    private void OnHorn(Entity<VehicleControlsComponent> ent, ref VehicleHornActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPredicted(ent.Comp.HornSound, ent.Owner, args.Performer);
    }

    private void OnSiren(Entity<VehicleControlsComponent> ent, ref VehicleSirenActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.SirenOn = !ent.Comp.SirenOn;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.SirenActionEntity, ent.Comp.SirenOn);
        UpdateSiren(ent);
    }

    private void OnExit(Entity<VehicleControlsComponent> ent, ref VehicleExitActionEvent args)
    {
        if (args.Handled
            || !_vehicle.TryGetOperatorContainer(ent.Owner, out var container)
            || !container.Contains(args.Performer))
            return;

        args.Handled = true;
        _container.Remove(args.Performer, container);
    }

    /// <summary>
    /// Toggles manning a turret: puts the first free gunner seat's gun in a free hand, or takes it away.
    /// Server-only, since it spawns the gun; the gun itself then fires through the normal gun system.
    /// </summary>
    private void OnManTurret(Entity<VehicleControlsComponent> ent, ref VehicleManTurretActionEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        args.Handled = true;
        var user = args.Performer;

        if (ReleaseTurret(ent, user))
            return;

        if (_vehicle.GetOperatorOrNull(ent.Owner) == user)
        {
            _popup.PopupEntity(Loc.GetString("vehicle-turret-driver"), user, user);
            return;
        }

        int? seat = null;
        for (var i = 0; i < ent.Comp.TurretWeapons.Count; i++)
        {
            if (ent.Comp.MannedTurrets.TryGetValue(i, out var manned) && !TerminatingOrDeleted(manned.Weapon))
                continue;

            seat = i;
            break;
        }

        if (seat is not { } freeSeat)
        {
            _popup.PopupEntity(Loc.GetString("vehicle-turret-all-manned"), user, user);
            return;
        }

        if (!_hands.TryGetEmptyHand(user, out var hand))
        {
            _popup.PopupEntity(Loc.GetString("vehicle-turret-hands-full"), user, user);
            return;
        }

        var weapon = Spawn(ent.Comp.TurretWeapons[freeSeat], Transform(ent).Coordinates);
        if (!_hands.TryPickup(user, weapon, hand, checkActionBlocker: false, animate: false))
        {
            Del(weapon);
            return;
        }

        _hands.SetActiveHand(user, hand);
        ent.Comp.MannedTurrets[freeSeat] = (user, weapon);
        if (ent.Comp.TurretActionEntities.TryGetValue(user, out var action))
            _actions.SetToggled(action, true);

        _popup.PopupEntity(Loc.GetString("vehicle-turret-manned", ("weapon", weapon)), user, user);
    }

    /// <summary>
    /// Frees the seat a gunner was manning and deletes the gun from their hand.
    /// Returns true if they were holding a live turret gun.
    /// </summary>
    private bool ReleaseTurret(Entity<VehicleControlsComponent> ent, EntityUid gunner)
    {
        foreach (var (seat, manned) in ent.Comp.MannedTurrets)
        {
            if (manned.Gunner != gunner)
                continue;

            // Safe: we stop enumerating right after removing.
            ent.Comp.MannedTurrets.Remove(seat);

            if (ent.Comp.TurretActionEntities.TryGetValue(gunner, out var action))
                _actions.SetToggled(action, false);

            if (TerminatingOrDeleted(manned.Weapon))
                return false;

            _popup.PopupEntity(Loc.GetString("vehicle-turret-released", ("weapon", manned.Weapon)), gunner, gunner);
            QueueDel(manned.Weapon);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clicking an enclosed vehicle with an empty hand climbs in, same as its Enter verb (drag-drop still works too).
    /// </summary>
    private void OnInteractHand(Entity<VehicleControlsComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !HasComp<ContainerVehicleEntryComponent>(ent) || IsAboard(ent, args.User))
            return;

        var enter = Loc.GetString("container-vehicle-verb-enter");
        foreach (var verb in _verbs.GetLocalVerbs(ent, args.User, typeof(AlternativeVerb)))
        {
            if (verb.Text != enter)
                continue;

            // The server runs it; the client executing it too would ask the server to run it a second time.
            args.Handled = true;
            if (_net.IsServer)
                _verbs.ExecuteVerb(verb, args.User, ent);
            return;
        }
    }

    #region Ignition

    /// <summary>
    /// Whether a keyed vehicle has a key that fits in its ignition. Unkeyed vehicles always do.
    /// </summary>
    public bool HasKey(EntityUid vehicle)
    {
        if (!TryComp<GenericKeyedVehicleComponent>(vehicle, out var keyed))
            return true;

        if (!_container.TryGetContainer(vehicle, keyed.ContainerId, out var slot))
            return false;

        foreach (var contained in slot.ContainedEntities)
        {
            if (_whitelist.IsWhitelistPass(keyed.KeyWhitelist, contained))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Pressing a direction in a vehicle that won't go tells the driver why, instead of nothing happening.
    /// </summary>
    private void OnMoveInput(Entity<VehicleControlsComponent> ent, ref MoveInputEvent args)
    {
        if (!_net.IsServer
            || !args.HasDirectionalMovement
            || args.Entity.Comp.CanMove
            || _timing.CurTime < ent.Comp.NextIgnitionHint
            || _vehicle.GetOperatorOrNull(ent.Owner) is not { } driver)
            return;

        string? msg = null;
        if (HasComp<EmpDisabledComponent>(ent))
            msg = "vehicle-stalled";
        else if (!HasKey(ent) && !_emag.CheckFlag(ent, EmagType.Interaction))
            msg = "vehicle-no-key";

        if (msg == null)
            return;

        ent.Comp.NextIgnitionHint = _timing.CurTime + IgnitionHintCooldown;
        _popup.PopupEntity(Loc.GetString(msg, ("vehicle", ent.Owner)), ent.Owner, driver, PopupType.Medium);
    }

    /// <summary>
    /// How to get in, and whether it'll start.
    /// </summary>
    private void ExamineIgnition(Entity<VehicleControlsComponent> ent, ref ExaminedEvent args)
    {
        if (HasComp<ContainerVehicleComponent>(ent))
            args.PushMarkup(Loc.GetString("vehicle-examine-board-enclosed"));
        else if (HasComp<StrapVehicleComponent>(ent))
            args.PushMarkup(Loc.GetString("vehicle-examine-board-ridden"));

        if (!HasComp<GenericKeyedVehicleComponent>(ent))
            return;

        if (HasKey(ent))
            args.PushMarkup(Loc.GetString("vehicle-examine-key-in"));
        else if (_emag.CheckFlag(ent, EmagType.Interaction))
            args.PushMarkup(Loc.GetString("vehicle-examine-hotwired"));
        else
            args.PushMarkup(Loc.GetString("vehicle-examine-key-missing"));
    }

    #endregion

    private void UpdateSiren(Entity<VehicleControlsComponent> ent)
    {
        _appearance.SetData(ent.Owner, VehicleSirenVisuals.On, ent.Comp.SirenOn);

        if (HasComp<AmbientSoundComponent>(ent))
            _ambient.SetAmbience(ent.Owner, ent.Comp.SirenOn);
    }
}
