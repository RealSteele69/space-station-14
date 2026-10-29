using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.GangConversion.Components;

/// <summary>
/// Marks an item (the gang tattoo gun) that recruits whoever it lands a melee hit on into the wielder's
/// gang. Mirrors <c>RevolutionaryConverterComponent</c>'s flash-conversion pattern, but off a direct hit
/// instead of a flash item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GangConverterComponent : Component
{
    /// <summary>
    /// Which gang this gun recruits for. Set when the gun is handed out; -1 means it hasn't been claimed yet.
    /// </summary>
    [DataField]
    public int GangId = -1;
}
