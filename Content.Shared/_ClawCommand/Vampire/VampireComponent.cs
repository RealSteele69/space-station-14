// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Alert;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// Marks an entity as a Vampire antagonist: an undead predator that survives on stolen blood,
/// grows in rank by spending it, and withers to ash in direct sunlight.
/// </summary>
[RegisterComponent]
public sealed partial class VampireComponent : Component
{
    /// <summary>
    /// Actions granted for free the moment this entity becomes a vampire (Feed, Masquerade, and - once
    /// ported - Veil of Many Faces). Everything else is learned via <see cref="PurchasablePowers"/>.
    /// </summary>
    [DataField]
    public List<EntProtoId> BaseVampireActions = new()
    {
        "ActionVampireFeed",
        "ActionVampireMasquerade",
        "ActionVampireEmbrace",
    };

    /// <summary>
    /// Powers learned by spending a rank - not granted for free. Resting in a claimed coffin with an
    /// unspent rank and enough blood lets the vampire pick one of whichever of these they don't own yet.
    /// </summary>
    [DataField]
    public List<EntProtoId> PurchasablePowers = new()
    {
        "ActionVampireFortitude",
        "ActionVampireCloak",
        "ActionVampireMesmerize",
    };

    [DataField]
    public HashSet<EntProtoId> PurchasedPowers = new();

    [DataField]
    public List<EntityUid>? ActionEntities;

    /// <summary>
    /// Blood stolen from victims. Spent on ranking up, slowly drained just by being awake and active.
    /// </summary>
    [DataField]
    public float BloodPoints = 100f;

    [DataField]
    public float MaxBloodPoints = 600f;

    /// <summary>
    /// Points of brute/toxin damage healed per regen tick, before blood-level scaling.
    /// </summary>
    [DataField]
    public float RegenRate = 0.3f;

    /// <summary>
    /// Blood points drained per tick from Feed, at rank 0.
    /// </summary>
    [DataField]
    public float FeedAmount = 15f;

    /// <summary>
    /// Blood points lost per second just from being awake, conscious and not Feeding.
    /// </summary>
    [DataField]
    public float PassiveBloodDrain = 0.1f;

    /// <summary>
    /// Points of brute/toxin damage healed per second, scaled by <see cref="RegenRate"/>. Stops entirely
    /// while Masquerading.
    /// </summary>
    [DataField]
    public ProtoId<DamageGroupPrototype> BruteHealGroup = "Brute";

    [DataField]
    public ProtoId<DamageGroupPrototype> ToxinHealGroup = "Toxin";

    /// <summary>
    /// Blood cost to toggle Masquerade on.
    /// </summary>
    [DataField]
    public float MasqueradeActivationCost = 10f;

    /// <summary>
    /// Extra blood points lost per second, on top of <see cref="PassiveBloodDrain"/>, while Masquerading.
    /// </summary>
    [DataField]
    public float MasqueradeDrainRate = 0.1f;

    [DataField]
    public float FortitudeActivationCost = 30f;

    /// <summary>
    /// Extra blood points lost per second, on top of <see cref="PassiveBloodDrain"/>, while Fortitude holds.
    /// </summary>
    [DataField]
    public float FortitudeDrainRate = 0.25f;

    [DataField]
    public EntProtoId FortitudeStatusEffect = "StatusEffectVampireFortitude";

    [DataField]
    public float CloakActivationCost = 5f;

    /// <summary>
    /// Extra blood points lost per second, on top of <see cref="PassiveBloodDrain"/>, while Cloaked.
    /// </summary>
    [DataField]
    public float CloakDrainRate = 0.04f;

    /// <summary>
    /// How visible the vampire stays while Cloak of Darkness is active (0 = fully hidden, 1 = fully visible).
    /// </summary>
    [DataField]
    public float CloakVisibility = 0.5f;

    /// <summary>
    /// The closet/crate this vampire has claimed as their coffin, if any. Resting inside it while closed
    /// heals far faster and costs no blood.
    /// </summary>
    [DataField]
    public EntityUid? ClaimedCoffin;

    [DataField]
    public ProtoId<DamageGroupPrototype> BurnHealGroup = "Burn";

    /// <summary>
    /// Multiplier applied to <see cref="RegenRate"/> while resting in a closed, owned coffin.
    /// </summary>
    [DataField]
    public float CoffinRegenMultiplier = 4f;

    /// <summary>
    /// Light-exposure tier (reusing the same 0-4 scale and thresholds Shadekin uses for their own
    /// photosensitivity) at and above which direct light starts burning the vampire. 0 and 1 (darkness
    /// and dim light) are always safe.
    /// </summary>
    [DataField]
    public int SunlightBurnTier = 2;

    /// <summary>
    /// Burn damage per second, per whole tier above <see cref="SunlightBurnTier"/>.
    /// </summary>
    [DataField]
    public float SunlightBurnPerTier = 1.5f;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool WarnedSunlight;

    [DataField]
    public float MesmerizeActivationCost = 30f;

    [DataField]
    public TimeSpan MesmerizeWindup = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan MesmerizeStunDuration = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Thralls bound to this vampire. Capacity is <see cref="Level"/> - a fledgling can't keep any.
    /// Freed the moment this vampire meets final death.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Vassals = new();

    [DataField]
    public TimeSpan EmbraceDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Distinct victims this vampire has fed on - tracked for the Blood Harvest objective.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> FedOn = new();

    /// <summary>
    /// How often the life tick (blood drain/regen/starvation/Feed) runs.
    /// </summary>
    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextTick;

    [DataField]
    public int Level;

    [DataField]
    public int LevelUnspent = 1;

    /// <summary>
    /// Fraction of <see cref="MaxBloodPoints"/> spent to rank up, regardless of which power (if any) is chosen.
    /// </summary>
    [DataField]
    public float LevelCostFraction = 0.2f;

    [DataField]
    public float RegenRateGrowth = 0.05f;

    [DataField]
    public float FeedAmountGrowth = 2f;

    [DataField]
    public float MaxBloodPointsGrowth = 100f;

    /// <summary>
    /// Rank at which a vampire first earns a true Title and swaps from the Fledgling to the Master
    /// reputation pool.
    /// </summary>
    [DataField]
    public int MasterLevel = 4;

    /// <summary>
    /// Has this vampire met their final death (decapitated, burned to ash, staked while helpless)?
    /// Once true, nothing can bring them back.
    /// </summary>
    [DataField]
    public bool FinalDeath;

    // Flavor name/title/reputation, in the style of "Count Dracul the Ravenous".
    // Title stays unset until the vampire first ranks up out of being a fledgling.

    /// <summary>
    /// Which name/title dataset pool was rolled at creation - kept so later title rolls stay consistent.
    /// </summary>
    [DataField]
    public bool IsMale;

    [DataField]
    public string? VampireName;

    [DataField]
    public string? VampireTitle;

    [DataField]
    public string? VampireReputation;

    [DataField]
    public ProtoId<DatasetPrototype> FirstNameMale = "names_vampire_first_male";

    [DataField]
    public ProtoId<DatasetPrototype> FirstNameFemale = "names_vampire_first_female";

    [DataField]
    public ProtoId<DatasetPrototype> TitleMale = "names_vampire_title_male";

    [DataField]
    public ProtoId<DatasetPrototype> TitleFemale = "names_vampire_title_female";

    [DataField]
    public ProtoId<DatasetPrototype> ReputationFledgling = "names_vampire_reputation_fledgling";

    [DataField]
    public ProtoId<DatasetPrototype> ReputationMaster = "names_vampire_reputation_master";

    /// <summary>
    /// Flat resistance applied on top of whatever species/clothing modifiers are already in play.
    /// </summary>
    [DataField]
    public ProtoId<DamageModifierSetPrototype> VampireDamageModifierSet = "VampireUndeadResilience";

    [DataField]
    public ProtoId<AlertPrototype> BloodAlert = "VampireBlood";
}
