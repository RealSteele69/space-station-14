using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - Extra driver/occupant controls for vehicles built on the upstream vehicle system.
/// The operator gets a horn and (optionally) a siren toggle; every occupant of a container vehicle
/// gets an exit action, and a passenger takes the wheel when the driver leaves.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleControlsComponent : Component
{
    /// <summary>
    /// Horn action granted to the operator. Null for no horn.
    /// </summary>
    [DataField]
    public EntProtoId? HornAction = "ActionVehicleCarHorn";

    [DataField, AutoNetworkedField]
    public EntityUid? HornActionEntity;

    [DataField]
    public SoundSpecifier HornSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/carhorn.ogg");

    /// <summary>
    /// Siren toggle granted to the operator. Null for no siren.
    /// The siren loop itself is the vehicle's AmbientSound, toggled alongside <see cref="VehicleSirenVisuals.On"/>.
    /// </summary>
    [DataField]
    public EntProtoId? SirenAction;

    [DataField, AutoNetworkedField]
    public EntityUid? SirenActionEntity;

    [DataField, AutoNetworkedField]
    public bool SirenOn;

    /// <summary>
    /// Exit action granted to every occupant of a container vehicle.
    /// </summary>
    [DataField]
    public EntProtoId? ExitAction = "ActionVehicleExit";

    /// <summary>
    /// Occupant -> their exit action. Server-side bookkeeping so actions are deleted on exit.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> ExitActionEntities = new();

    /// <summary>
    /// Most entities the occupant container may hold, driver included. Null for no limit.
    /// </summary>
    [DataField]
    public int? MaxOccupants = 1;

    /// <summary>
    /// Turret guns, one per gunner seat. A passenger (never the driver) mans a free seat with
    /// <see cref="TurretAction"/>, which puts that seat's gun in their hand until they let go or leave.
    /// </summary>
    [DataField]
    public List<EntProtoId> TurretWeapons = new();

    [DataField]
    public EntProtoId TurretAction = "ActionVehicleManTurret";

    /// <summary>
    /// Occupant -> their man-turret action. Server-side bookkeeping.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> TurretActionEntities = new();

    /// <summary>
    /// Gunner seat index -> (gunner, gun in their hand). Server-side bookkeeping.
    /// </summary>
    [ViewVariables]
    public Dictionary<int, (EntityUid Gunner, EntityUid Weapon)> MannedTurrets = new();

    #region Maintenance (screwdriver opens the panel, then weld it back together)

    /// <summary>Welding repairs (RepairableComponent) need the maintenance panel open.</summary>
    [DataField]
    public bool RequirePanelForRepair = true;

    [DataField, AutoNetworkedField]
    public bool PanelOpen;

    [DataField]
    public float PanelDelay = 1.5f;

    #endregion

    #region Damage

    /// <summary>
    /// Bullets and lasers hit the hull (it must still be aimed at, like crates and machines).
    /// Shots fired from aboard never hit their own vehicle.
    /// </summary>
    [DataField]
    public bool Shootable = true;

    /// <summary>
    /// Open vehicles: chance a bullet aimed at the vehicle carries on into a rider instead (open-topped trains: 70%).
    /// </summary>
    [DataField]
    public float RiderHitChance = 0.7f;

    /// <summary>Total damage at which the vehicle shows wear (damaged frame overlays).</summary>
    [DataField]
    public float DamagedThreshold = 100f;

    /// <summary>Total damage at which the vehicle is in a bad way: it sparks and the frame overlay stays up.</summary>
    [DataField]
    public float CriticalThreshold = 160f;

    [DataField, AutoNetworkedField]
    public VehicleWearLevel Wear = VehicleWearLevel.Fine;

    [ViewVariables]
    public TimeSpan NextWearCheck;

    [ViewVariables]
    public TimeSpan NextSpark;

    /// <summary>When the driver may next be told why the vehicle won't go.</summary>
    [ViewVariables]
    public TimeSpan NextIgnitionHint;

    /// <summary>Whether the last run-state refresh saw the engine stalled by an EMP.</summary>
    [ViewVariables]
    public bool Stalled;

    #endregion

    #region Emag

    /// <summary>
    /// Emagged ("safeties off"): keyed vehicles hotwire, the speed limiter goes,
    /// and people get hit much harder and flung ahead instead of aside.
    /// </summary>
    [DataField]
    public float EmagTopSpeedBonus = 0.3f;

    [DataField]
    public float EmagRunOverMultiplier = 1.5f;

    #endregion
}

[Serializable, NetSerializable]
public enum VehicleWearLevel : byte
{
    Fine,
    Damaged,
    Critical,
}

[Serializable, NetSerializable]
public enum VehicleWearVisuals : byte
{
    Level,
}

[Serializable, NetSerializable]
public sealed partial class VehiclePanelDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class VehicleHornActionEvent : InstantActionEvent;

public sealed partial class VehicleSirenActionEvent : InstantActionEvent;

public sealed partial class VehicleExitActionEvent : InstantActionEvent;

public sealed partial class VehicleManTurretActionEvent : InstantActionEvent;

public sealed partial class VehicleReverseActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum VehicleSirenVisuals : byte
{
    On,
}
