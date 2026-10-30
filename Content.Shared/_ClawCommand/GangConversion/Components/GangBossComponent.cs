using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.GangConversion.Components;

/// <summary>
/// Marks the founder and leader of a gang. Only a boss can promote a fellow member to enforcer.
/// Always accompanied by <see cref="GangMemberComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GangBossComponent : Component
{
    /// <summary>
    /// The status icon shown for the gang boss instead of the regular <see cref="GangMemberComponent.StatusIcon"/>.
    /// </summary>
    [DataField]
    public ProtoId<StatusIconPrototype> StatusIcon = "GangBoss";
}
