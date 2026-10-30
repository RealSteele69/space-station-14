using Content.Shared.Body;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.BloodCult.Components;

/// <summary>
/// Marks a member of the Blood Cult. Removing it deconverts the entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodCultistComponent : Component
{
    [DataField]
    public ProtoId<StatusIconPrototype> StatusIcon = "BloodCultMember";

    /// <summary>
    /// How long holy water takes to purge the cult out of someone.
    /// </summary>
    [DataField]
    public TimeSpan PurifyTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The blood spear bound to this cultist, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? BloodSpear;

    /// <summary>
    /// Organ profiles from before the cult turned this cultist's eyes red, used to restore them.
    /// </summary>
    [ViewVariables]
    public Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>? OriginalProfiles;
}

/// <summary>
/// The cult leader, picked once the cult grows large enough for halos.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodCultLeaderComponent : Component
{
    [DataField]
    public ProtoId<StatusIconPrototype> StatusIcon = "BloodCultLeader";
}

/// <summary>
/// Draws a floating halo above a cultist once the cult has grown large enough to reveal itself.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodCultHaloComponent : Component
{
    [DataField]
    public string RsiPath = "/Textures/_ClawCommand/BloodCult/Effects/pentagram.rsi";

    [DataField]
    public List<string> States = new() { "halo1", "halo2", "halo3", "halo4", "halo5", "halo6" };
}
