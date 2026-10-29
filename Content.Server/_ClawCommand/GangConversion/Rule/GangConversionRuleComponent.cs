using Content.Shared.Dataset;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.GangConversion.Rule;

/// <summary>
/// The Gang Conversion game rule. Every selected boss founds their own gang; gangs recruit crew by force
/// and fight over turf by spray-tagging the station. The gang with the most turf (and, on a tie, the most
/// living members) at round end wins.
/// </summary>
[RegisterComponent, Access(typeof(GangConversionRuleSystem))]
public sealed partial class GangConversionRuleComponent : Component
{
    [DataField]
    public ProtoId<NpcFactionPrototype> NanoTrasenFaction = "NanoTrasen";

    [DataField]
    public EntProtoId BossMindRole = "MindRoleGangBoss";

    [DataField]
    public EntProtoId MemberMindRole = "MindRoleGangMember";

    [DataField]
    public EntProtoId ConverterItem = "GangTattooGun";

    [DataField]
    public EntProtoId TaggerItem = "GangSprayTagger";

    [DataField]
    public ProtoId<DatasetPrototype> NameDataset = "GangNames";

    /// <summary>
    /// The decal prototype gang taggers paint, used when scanning the map for turf scores.
    /// </summary>
    [DataField]
    public string TagDecalId = "GangTagDecal";

    /// <summary>
    /// How often turf is rescored while the rule is active.
    /// </summary>
    [DataField]
    public TimeSpan TurfTickInterval = TimeSpan.FromSeconds(20);

    [DataField]
    public SoundSpecifier ConvertSound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");

    [DataField]
    public SoundSpecifier DeconvertSound = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/repulse.ogg");

    /// <summary>
    /// Radio channels handed out to gangs in the order they're founded. Caps the number of simultaneous
    /// gangs at four, matching <see cref="TeamColors"/>.
    /// </summary>
    [DataField]
    public List<ProtoId<RadioChannelPrototype>> Channels = new()
    {
        "GangAlphaRadio",
        "GangBetaRadio",
        "GangGammaRadio",
        "GangDeltaRadio",
    };

    [DataField]
    public List<Color> TeamColors = new()
    {
        Color.FromHex("#c62828"), // red
        Color.FromHex("#1565c0"), // blue
        Color.FromHex("#2e7d32"), // green
        Color.FromHex("#6a1b9a"), // purple
    };

    [ViewVariables]
    public List<GangTeamData> Teams = new();

    [ViewVariables]
    public TimeSpan NextTurfTick;
}

/// <summary>
/// Everything the rule tracks about one gang for the round.
/// </summary>
public sealed class GangTeamData
{
    public required int Id;
    public required string Name;
    public required Color Color;
    public required ProtoId<RadioChannelPrototype> Channel;

    public EntityUid? Boss;
    public string BossName = string.Empty;

    public HashSet<EntityUid> Members = new();

    /// <summary>
    /// Everyone who was ever part of this gang, for the round end summary.
    /// </summary>
    public Dictionary<EntityUid, string> Roster = new();

    public int TurfScore;
    public int PeakMembers;
}
