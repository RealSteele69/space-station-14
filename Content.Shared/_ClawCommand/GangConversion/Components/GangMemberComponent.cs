using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.GangConversion.Components;

/// <summary>
/// Marks an entity as belonging to a gang this round. Removing it (death, deconversion) drops the member
/// from their gang's roster. The boss and any promoted enforcers also carry this, alongside their own
/// rank-specific marker component.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GangMemberComponent : Component
{
    /// <summary>
    /// Which gang this round (<see cref="Rule.GangConversionRuleComponent.Teams"/> index) the entity belongs to.
    /// </summary>
    [DataField]
    public int GangId = -1;

    /// <summary>
    /// This gang's turf colour, cached here so UI/status code doesn't need to go through the rule.
    /// </summary>
    [DataField]
    public Color GangColor;

    [DataField]
    public ProtoId<StatusIconPrototype> StatusIcon = "GangMember";
}
