using Content.Shared.Chat;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.BloodCult.Runes;

/// <summary>
/// Any cult rune. Invoked by a cultist activating it, with enough cultists standing around it.
/// </summary>
[RegisterComponent]
public sealed partial class CultRuneBaseComponent : Component
{
    [DataField(required: true)]
    public string InvokePhrase = "";

    [DataField]
    public InGameICChatType InvokeChatType = InGameICChatType.Whisper;

    [DataField]
    public int RequiredInvokers = 1;

    [DataField]
    public float RuneActivationRange = 1f;

    /// <summary>
    /// Damage dealt to every invoker on activation.
    /// </summary>
    [DataField]
    public DamageSpecifier? ActivationDamage;

    /// <summary>
    /// Final runes use up the thin spot in the veil they were drawn on.
    /// </summary>
    [DataField]
    public bool TriggerRendingMarkers;

    [DataField]
    public bool CanBeErased = true;

    [DataField]
    public ProtoId<ReagentPrototype> HolyWater = "HolyWater";
}

/// <summary>
/// The ritual dagger: scribes runes and scrubs them away.
/// </summary>
[RegisterComponent]
public sealed partial class RuneDrawerComponent : Component
{
    [DataField]
    public TimeSpan EraseTime = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier StartDrawingSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/bloodcult_scribe.ogg");

    [DataField]
    public SoundSpecifier EndDrawingSound = new SoundCollectionSpecifier("blood");
}

/// <summary>
/// A spot where the veil is thin. A few of these are picked each round as the only places Nar'Sie can be called.
/// </summary>
[RegisterComponent]
public sealed partial class RendingRunePlacementMarkerComponent : Component
{
    [DataField]
    public bool IsActive;

    [DataField]
    public float DrawingRange = 10f;
}

[RegisterComponent]
public sealed partial class CultRuneOfferingComponent : Component
{
    [DataField]
    public float OfferingRange = 0.5f;

    /// <summary>
    /// Cultists needed to convert a living target.
    /// </summary>
    [DataField]
    public int ConvertInvokersAmount = 2;

    /// <summary>
    /// Cultists needed to sacrifice a living target that can't be converted.
    /// </summary>
    [DataField]
    public int AliveSacrificeInvokersAmount = 3;

    [DataField]
    public int ReviveChargesPerOffering = 1;

    [DataField]
    public EntProtoId SoulShardProto = "SoulShard";

    [DataField]
    public EntProtoId SoulShardGhostProto = "SoulShardGhost";

    [DataField]
    public DamageSpecifier ConvertHealing = new()
    {
        DamageDict = new()
        {
            ["Blunt"] = -20,
            ["Slash"] = -20,
            ["Piercing"] = -20,
            ["Heat"] = -20,
            ["Cold"] = -10,
            ["Shock"] = -10,
        },
    };
}

[RegisterComponent]
public sealed partial class CultRuneEmpowerComponent : Component;

[RegisterComponent]
public sealed partial class CultRuneTeleportComponent : Component
{
    [DataField]
    public float TeleportGatherRange = 0.65f;

    [DataField]
    public string Name = "";

    [DataField]
    public SoundSpecifier TeleportInSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/wand_teleport.ogg");

    [DataField]
    public SoundSpecifier TeleportOutSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/wand_teleport.ogg");
}

[RegisterComponent]
public sealed partial class CultRuneReviveComponent : Component
{
    [DataField]
    public float ReviveRange = 0.5f;

    [DataField]
    public DamageSpecifier Healing = new()
    {
        DamageDict = new()
        {
            ["Blunt"] = -100,
            ["Slash"] = -100,
            ["Piercing"] = -100,
            ["Heat"] = -100,
            ["Cold"] = -100,
            ["Shock"] = -100,
            ["Asphyxiation"] = -100,
            ["Bloodloss"] = -100,
            ["Poison"] = -50,
            ["Cellular"] = -50,
        },
    };
}

/// <summary>
/// Revive charges, shared by every revive rune on the map. Offerings add more.
/// </summary>
[RegisterComponent]
public sealed partial class ReviveRuneChargesProviderComponent : Component
{
    [DataField]
    public int Charges = 3;
}

[RegisterComponent]
public sealed partial class CultRuneBarrierComponent : Component
{
    [DataField]
    public EntProtoId SpawnPrototype = "BloodCultBarrier";
}

[RegisterComponent]
public sealed partial class CultRuneSummonComponent : Component
{
    [DataField]
    public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/wand_teleport.ogg");
}

[RegisterComponent]
public sealed partial class CultRuneBloodBoilComponent : Component
{
    [DataField]
    public EntProtoId ProjectilePrototype = "BloodBoilProjectile";

    [DataField]
    public float ProjectileSpeed = 50f;

    [DataField]
    public float TargetsLookupRange = 15f;

    [DataField]
    public int ProjectileCount = 3;

    [DataField]
    public float FireStacksPerProjectile = 1f;

    [DataField]
    public SoundSpecifier ActivationSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/magic_missile.ogg");
}

[RegisterComponent]
public sealed partial class CultRuneApocalypseComponent : Component
{
    [DataField]
    public TimeSpan InvokeTime = TimeSpan.FromSeconds(20);

    /// <summary>
    /// If the cult makes up less than this share of the players, one of the possible events also happens.
    /// </summary>
    [DataField]
    public float CultistsThreshold = 0.15f;

    [DataField]
    public float EmpRange = 30f;

    [DataField]
    public float EmpEnergyConsumption = 10000f;

    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(180);

    [DataField]
    public bool Used;

    /// <summary>
    /// Events started on every activation.
    /// </summary>
    [DataField]
    public List<EntProtoId> GuaranteedEvents = new()
    {
        "PowerGridCheck",
        "SolarFlare",
    };

    /// <summary>
    /// One of these is picked when the cult is struggling, started the given number of times.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> PossibleEvents = new()
    {
        ["ImmovableRodSpawn"] = 3,
        ["MimicVendorRule"] = 2,
        ["MouseMigration"] = 2,
        ["MeteorSwarmMedium"] = 2,
        ["SpiderSpawn"] = 3,
        ["AnomalySpawn"] = 4,
        ["KudzuGrowth"] = 2,
    };

    public DoAfterId? CurrentDoAfter;
}

[RegisterComponent]
public sealed partial class CultRuneRendingComponent : Component
{
    [DataField]
    public TimeSpan SummonTime = TimeSpan.FromSeconds(40);

    [DataField]
    public SoundSpecifier FinishedDrawingAudio = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/bloodcult_halos.ogg");

    [DataField]
    public SoundSpecifier SummonAudio = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/cultist_chant.ogg");

    [DataField]
    public EntProtoId NarsiePrototype = "MobNarsieSpawn";

    public DoAfterId? CurrentDoAfter;

    public EntityUid? AudioEntity;
}

/// <summary>
/// Raised on a rune when a cultist tries to invoke it. Cancel to stop the invocation and its costs.
/// </summary>
public sealed class TryInvokeCultRuneEvent(EntityUid user, HashSet<EntityUid> invokers) : CancellableEntityEventArgs
{
    public readonly EntityUid User = user;
    public readonly HashSet<EntityUid> Invokers = invokers;
}

/// <summary>
/// Raised on a rune right after it has been scribed.
/// </summary>
[ByRefEvent]
public readonly record struct AfterRunePlacedEvent(EntityUid User);
