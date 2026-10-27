using Content.Shared.Shuttles.Events;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class NavInterfaceState
{
    public float MaxRange;

    /// <summary>
    /// The relevant coordinates to base the radar around.
    /// </summary>
    public NetCoordinates? Coordinates;

    /// <summary>
    /// The relevant rotation to rotate the angle around.
    /// </summary>
    public Angle? Angle;

    public Dictionary<NetEntity, List<DockingPortState>> Docks;

    public bool RotateWithEntity = true;

    /// <summary>
    /// The shuttle's current inertial dampener mode.
    /// </summary>
    public InertiaDampeningMode DampeningMode;

    public NavInterfaceState(
        float maxRange,
        NetCoordinates? coordinates,
        Angle? angle,
        Dictionary<NetEntity, List<DockingPortState>> docks,
        InertiaDampeningMode dampeningMode = InertiaDampeningMode.Dampened)
    {
        MaxRange = maxRange;
        Coordinates = coordinates;
        Angle = angle;
        Docks = docks;
        DampeningMode = dampeningMode;
    }
}

[Serializable, NetSerializable]
public enum RadarConsoleUiKey : byte
{
    Key
}
