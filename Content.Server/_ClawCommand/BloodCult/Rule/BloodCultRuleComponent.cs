using Content.Shared.NPC.Prototypes;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.BloodCult.Rule;

/// <summary>
/// The Blood Cult game rule. The cult has to sacrifice a chosen crew member and then summon Nar'Sie.
/// </summary>
[RegisterComponent, Access(typeof(BloodCultRuleSystem))]
public sealed partial class BloodCultRuleComponent : Component
{
    [DataField]
    public ProtoId<NpcFactionPrototype> NanoTrasenFaction = "NanoTrasen";

    [DataField]
    public ProtoId<NpcFactionPrototype> BloodCultFaction = "GeometerOfBlood";

    [DataField]
    public ProtoId<RadioChannelPrototype> CultChannel = "BloodCult";

    [DataField]
    public EntProtoId MindRole = "MindRoleBloodCultist";

    [DataField]
    public EntProtoId Objective = "KillTargetCultObjective";

    [DataField]
    public EntProtoId SummonObjective = "SummonNarsieObjective";

    /// <summary>
    /// Every cultist turns into one of these when Nar'Sie rises.
    /// </summary>
    [DataField]
    public EntProtoId HarvesterPrototype = "ConstructHarvester";

    [DataField]
    public EntProtoId SpellsAction = "ActionBloodCultPrepareSpells";

    [DataField]
    public Color EyeColor = Color.FromHex("#f80000");

    /// <summary>
    /// Cult size at which every cultist's eyes start glowing red.
    /// </summary>
    [DataField]
    public int RedEyesThreshold = 5;

    /// <summary>
    /// Cult size at which every cultist gets a halo and a leader is chosen.
    /// </summary>
    [DataField]
    public int HaloThreshold = 8;

    /// <summary>
    /// How many places on the station the veil is thin enough to draw the final runes.
    /// </summary>
    [DataField]
    public int RendingRunePlacementsAmount = 3;

    [DataField]
    public SoundSpecifier ConvertSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/bloodcult_gain.ogg");

    [DataField]
    public SoundSpecifier RedEyesSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/bloodcult_eyes.ogg");

    [DataField]
    public SoundSpecifier HaloSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/bloodcult_halos.ogg");

    [DataField]
    public SoundSpecifier DeconvertSound = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/repulse.ogg");

    /// <summary>
    /// If no rending markers exist on the map, the cult may instead draw this many final runes anywhere.
    /// </summary>
    [ViewVariables]
    public bool EmergencyMarkersMode;

    [ViewVariables]
    public int EmergencyMarkersCount;

    /// <summary>
    /// The crew member who has to be sacrificed before Nar'Sie can be summoned.
    /// </summary>
    [ViewVariables]
    public EntityUid? OfferingTarget;

    [ViewVariables]
    public EntityUid? CultLeader;

    [ViewVariables]
    public bool LeaderSelected;

    [ViewVariables]
    public BloodCultStage Stage = BloodCultStage.Start;

    [ViewVariables]
    public BloodCultWinCondition WinCondition = BloodCultWinCondition.Draw;

    [ViewVariables]
    public HashSet<EntityUid> Cultists = new();

    /// <summary>
    /// Everyone who was ever part of the cult, for the round end summary.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, string> Roster = new();

    [ViewVariables]
    public int TotalConverted;

    [ViewVariables]
    public int TotalSacrificed;
}

public enum BloodCultWinCondition : byte
{
    Draw,
    Win,
    Failure,
}

public enum BloodCultStage : byte
{
    Start,
    RedEyes,
    Halos,
}

/// <summary>
/// Raised broadcast when the Dimensional Rending ritual completes.
/// </summary>
public sealed class BloodCultNarsieSummonedEvent : EntityEventArgs;
