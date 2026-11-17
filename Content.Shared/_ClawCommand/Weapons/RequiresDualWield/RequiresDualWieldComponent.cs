// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Weapons.RequiresDualWield;

/// <summary>
/// Makes a gun only able to be shot while its sister gun is held in the other hand.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(RequiresDualWieldSystem))]
public sealed partial class RequiresDualWieldComponent : Component
{
    public TimeSpan LastPopup;

    [DataField]
    public TimeSpan PopupCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public LocId? WieldRequiresExamineMessage = "gun-requires-dual-wield-component-examine";

    /// <summary>
    /// What the item in the other hand has to be.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;
}
