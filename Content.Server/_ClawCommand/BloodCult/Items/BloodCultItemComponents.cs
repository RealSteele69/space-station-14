using Content.Shared._ClawCommand.BloodCult;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.BloodCult.Items;

/// <summary>
/// Blood halberd. Binds to the first cultist who picks it up, who can then recall it to their hand.
/// </summary>
[RegisterComponent]
public sealed partial class BloodSpearComponent : Component
{
    [ViewVariables]
    public EntityUid? Master;

    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromSeconds(4);

    [DataField]
    public EntProtoId RecallActionId = "ActionBloodSpearRecall";

    [ViewVariables]
    public EntityUid? RecallAction;

    [DataField]
    public SoundSpecifier RecallAudio = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/blood3.ogg");
}

/// <summary>
/// Shatter to delay the evacuation shuttle. Each map only tolerates a few curses.
/// </summary>
[RegisterComponent]
public sealed partial class ShuttleCurseComponent : Component
{
    [DataField]
    public TimeSpan DelayTime = TimeSpan.FromMinutes(3);

    [DataField]
    public SoundSpecifier ScatterSound = new SoundCollectionSpecifier("GlassBreak");

    [DataField]
    public List<LocId> CurseMessages = new()
    {
        "shuttle-curse-message-1",
        "shuttle-curse-message-2",
        "shuttle-curse-message-3",
        "shuttle-curse-message-4",
        "shuttle-curse-message-5",
        "shuttle-curse-message-6",
        "shuttle-curse-message-7",
        "shuttle-curse-message-8",
        "shuttle-curse-message-9",
        "shuttle-curse-message-10",
        "shuttle-curse-message-11",
    };
}

/// <summary>
/// Tracks how many shuttle curses were used on a map.
/// </summary>
[RegisterComponent]
public sealed partial class ShuttleCurseProviderComponent : Component
{
    [DataField]
    public int MaxUses = 3;

    [ViewVariables]
    public int CurrentUses;
}

/// <summary>
/// Blinks the user, and whatever they are pulling, a few tiles forward.
/// </summary>
[RegisterComponent]
public sealed partial class VeilShifterComponent : Component
{
    [DataField]
    public int Charges = 4;

    [DataField]
    public int TeleportDistanceMax = 10;

    [DataField]
    public int TeleportDistanceMin = 5;

    /// <summary>
    /// How many spots to try before giving up.
    /// </summary>
    [DataField]
    public int Attempts = 10;

    [DataField]
    public SoundSpecifier TeleportInSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/enter_blood.ogg");

    [DataField]
    public SoundSpecifier TeleportOutSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/exit_blood.ogg");

    [DataField]
    public EntProtoId TeleportInEffect = "CultTeleportInEffect";

    [DataField]
    public EntProtoId TeleportOutEffect = "CultTeleportOutEffect";
}

/// <summary>
/// Sends an item straight into another cultist's hands.
/// </summary>
[RegisterComponent]
public sealed partial class VoidTorchComponent : Component
{
    [DataField]
    public int Charges = 5;

    [DataField]
    public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/wand_teleport.ogg");

    [ViewVariables]
    public EntityUid? TargetItem;
}

/// <summary>
/// A cult structure that hands out an item from a list, then needs a while to recharge.
/// </summary>
[RegisterComponent]
public sealed partial class TimedFactoryComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Entries = new();

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(240);

    [ViewVariables]
    public TimeSpan NextUse;
}

/// <summary>
/// The barrier from the barrier rune. Cultists dispel it with their dagger.
/// </summary>
[RegisterComponent]
public sealed partial class BloodCultBarrierComponent : Component;

/// <summary>
/// Thrown, this passes harmlessly through cult members.
/// </summary>
[RegisterComponent]
public sealed partial class BloodCultPassThroughCultComponent : Component;

/// <summary>
/// Blood boil bolts only hit the victim they were cast at.
/// </summary>
[RegisterComponent]
public sealed partial class BloodBoilProjectileComponent : Component
{
    [ViewVariables]
    public EntityUid Target;
}

/// <summary>
/// Sharpens melee weapons up to a cap.
/// </summary>
[RegisterComponent]
public sealed partial class WhetstoneComponent : Component
{
    [DataField]
    public int Uses = 1;

    [DataField]
    public DamageSpecifier DamageIncrease = new() { DamageDict = new() { ["Slash"] = 4 } };

    [DataField]
    public FixedPoint2 MaximumIncrease = 25;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public SoundSpecifier SharpenAudio = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/sheath.ogg");
}

#region Hand auras

/// <summary>
/// A blood spell held in hand. Whispers its incantation when it lands.
/// </summary>
public abstract partial class BaseBloodCultAuraComponent : Component
{
    [DataField]
    public string? Speech;
}

[RegisterComponent]
public sealed partial class StunAuraComponent : BaseBloodCultAuraComponent
{
    [DataField]
    public TimeSpan ParalyzeDuration = TimeSpan.FromSeconds(16);

    [DataField]
    public TimeSpan MuteDuration = TimeSpan.FromSeconds(12);
}

[RegisterComponent]
public sealed partial class ShadowShacklesAuraComponent : BaseBloodCultAuraComponent
{
    [DataField]
    public EntProtoId ShacklesProto = "ShadowShackles";

    [DataField]
    public TimeSpan MuteDuration = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public EntityUid? Shackles;

    [ViewVariables]
    public EntityUid? Target;
}

[RegisterComponent]
public sealed partial class BloodRitesAuraComponent : BaseBloodCultAuraComponent
{
    [DataField]
    public FixedPoint2 StoredBlood;

    /// <summary>
    /// Blood cost per unit of blood restored to another cultist.
    /// </summary>
    [DataField]
    public float BloodRegenerationRatio = 0.1f;

    /// <summary>
    /// Healing yourself costs this many times more.
    /// </summary>
    [DataField]
    public float SelfHealRatio = 2f;

    [DataField]
    public FixedPoint2 BloodExtractionAmount = 30;

    [DataField]
    public TimeSpan BloodExtractionTime = TimeSpan.FromSeconds(5);

    [DataField]
    public FixedPoint2 HealingCost = 40;

    [DataField]
    public FixedPoint2 TotalHealing = 20;

    [DataField]
    public float PuddleConsumeRadius = 0.5f;

    [DataField]
    public SoundSpecifier BloodRitesAudio = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/blood3.ogg");

    /// <summary>
    /// Advanced rites: entity and its blood cost.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, FixedPoint2> Crafts = new()
    {
        ["BloodSpear"] = 300,
    };

    public bool Extracting;
}

#endregion
