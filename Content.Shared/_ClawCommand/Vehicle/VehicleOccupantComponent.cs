using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - Marks someone riding inside an enclosed vehicle. They can only fire turret guns
/// (<see cref="VehicleTurretWeaponComponent"/>) from in there, not their own weapons.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleOccupantComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;
}

/// <summary>
/// Claw Command - Marks a vehicle turret gun, the only kind of gun usable from inside an enclosed vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleTurretWeaponComponent : Component;
