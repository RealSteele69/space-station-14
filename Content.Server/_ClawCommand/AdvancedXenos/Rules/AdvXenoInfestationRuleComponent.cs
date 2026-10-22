using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.AdvancedXenos.Rules;

/// <summary>
/// Claw Command - the xeno infestation event: weed nodes sprout from every vent in one part of the
/// station and NPC xenomorphs crawl out of them, scaled by how many people are aboard.
/// </summary>
[RegisterComponent, Access(typeof(AdvXenoInfestationRuleSystem))]
public sealed partial class AdvXenoInfestationRuleComponent : Component
{
    /// <summary>one player = 2 points</summary>
    [DataField]
    public int Strength = 2;

    /// <summary>adds points of aliens independent of player count</summary>
    [DataField]
    public int AdditionalPlayers = 3;

    /// <summary>Vents within this many tiles of the chosen vent count as "the area".</summary>
    [DataField]
    public float AreaRadius = 12f;

    [DataField]
    public EntProtoId WeedNode = "AdvXenoWeedNode";

    [DataField]
    public EntProtoId Praetorian = "MobAdvXenoBiomorphVanguard";

    [DataField]
    public EntProtoId Sentinel = "MobAdvXenoBiomorphSpitter";

    [DataField]
    public EntProtoId Drone = "MobAdvXenoBiomorphDrone";

    [DataField]
    public EntProtoId Hunter = "MobAdvXenoBiomorphWarrior";

    [DataField]
    public List<EntityCoordinates> Vents = new();

    [DataField]
    public string LocationName = string.Empty;
}
