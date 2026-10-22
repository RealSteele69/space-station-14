// Claw Command - advanced xenomorph abilities.

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.AdvancedXenos;

/// <summary>Plant Weeds (50): a weed node on your tile.</summary>
public sealed partial class AdvXenoPlantWeedsActionEvent : InstantActionEvent
{
    [DataField]
    public int Cost = 50;

    [DataField]
    public EntProtoId Node = "AdvXenoWeedNode";
}

/// <summary>Secrete Resin (75): shape a resin structure on an adjacent tile.</summary>
public sealed partial class AdvXenoSecreteResinActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public int Cost = 75;

    /// <summary>The structure is solid, so the tile must be free of anything solid (including people).</summary>
    [DataField]
    public bool Solid = true;

    /// <summary>Shown to the user, e.g. "resin wall".</summary>
    [DataField]
    public LocId ShapeName = "adv-xeno-resin-wall";
}

/// <summary>Lay Egg (75): queen only, one egg per tile.</summary>
public sealed partial class AdvXenoLayEggActionEvent : InstantActionEvent
{
    [DataField]
    public int Cost = 75;

    [DataField]
    public EntProtoId Egg = "AdvXenoEgg";
}

/// <summary>Corrosive Acid (200).</summary>
public sealed partial class AdvXenoCorrosiveAcidActionEvent : EntityTargetActionEvent
{
    [DataField]
    public int Cost = 200;

    [DataField]
    public EntProtoId Acid = "AdvXenoAcid";
}

/// <summary>Neurotoxic / acid spit (20 per spit).</summary>
public sealed partial class AdvXenoSpitActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public int Cost = 20;

    [DataField]
    public LocId SpitName = "adv-xeno-spit-neurotoxin";
}

/// <summary>Transfer Plasma to an adjacent alien.</summary>
public sealed partial class AdvXenoTransferPlasmaActionEvent : EntityTargetActionEvent;

/// <summary>Evolve into another form: larva to caste (needs full growth), drone to queen (500 plasma, one queen).</summary>
public sealed partial class AdvXenoEvolveActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Into;

    [DataField]
    public int Cost;

    [DataField]
    public bool RequiresGrowth;

    /// <summary>Refused while a queen is alive and played.</summary>
    [DataField]
    public bool UniqueQueen;
}

/// <summary>Hunter: Leap at a target and grab them.</summary>
public sealed partial class AdvXenoLeapActionEvent : EntityTargetActionEvent
{
    [DataField]
    public float MaxDistance = 4f;
}

/// <summary>Hunter: rip at whoever you are holding in an aggressive grab.</summary>
public sealed partial class AdvXenoGutActionEvent : InstantActionEvent
{
    [DataField]
    public float Damage = 50f;
}

/// <summary>Hunter/Sentinel: Tackle someone down.</summary>
public sealed partial class AdvXenoTackleActionEvent : EntityTargetActionEvent;

/// <summary>Hunter/Queen: Psychic Whisper.</summary>
public sealed partial class AdvXenoPsychicWhisperActionEvent : EntityTargetActionEvent;

/// <summary>Devour whoever you hold in an aggressive grab.</summary>
public sealed partial class AdvXenoDevourActionEvent : EntityTargetActionEvent;

/// <summary>Regurgitate: empty your stomach.</summary>
public sealed partial class AdvXenoRegurgitateActionEvent : InstantActionEvent;

/// <summary>Larva: Hide under tables.</summary>
public sealed partial class AdvXenoHideActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class AdvXenoDevourDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class AdvXenoNestResistDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class AdvXenoEmbryoSurgeryDoAfterEvent : SimpleDoAfterEvent;
