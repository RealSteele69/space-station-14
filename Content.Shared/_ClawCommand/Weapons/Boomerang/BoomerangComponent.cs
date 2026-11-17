// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Weapons.Boomerang;

/// <summary>
/// Makes a thrown entity fly back to whoever threw it after it lands.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoomerangComponent : Component
{
    /// <summary>
    /// Entity we should return to after landing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Thrower;

    /// <summary>
    /// Distance to the thrower at which we try to get caught.
    /// </summary>
    [DataField]
    public float PickupDistance = 1.5f;

    /// <summary>
    /// Speed we return at.
    /// </summary>
    [DataField]
    public float ReturnSpeed = 10f;

    /// <summary>
    /// Maximum return hops we can make.
    /// </summary>
    [DataField]
    public int MaxHops = 6;

    /// <summary>
    /// Return hops made so far.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentHops;
}
