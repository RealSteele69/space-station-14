using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Jacqueline the Pumpqueen: a wandering spooky-season NPC who trades candy for a random treat -
/// or, sometimes, a trick.
/// </summary>
[RegisterComponent]
public sealed partial class JacquelineComponent : Component
{
    /// <summary>
    /// What she accepts as payment for a trade.
    /// </summary>
    [DataField]
    public EntProtoId Payment = "WitchesCandy";

    /// <summary>
    /// Random reward pool for a successful "treat" trade.
    /// </summary>
    [DataField]
    public List<EntProtoId> Treats = new();

    /// <summary>
    /// Chance that a trade is a "trick" (forces a cursed costume on the payer) instead of a treat.
    /// </summary>
    [DataField]
    public float TrickChance = 0.2f;

    [DataField]
    public EntProtoId TrickCostume = "ClothingOuterGhostSheet";

    [DataField]
    public EntProtoId TrickHat = "ClothingHeadHatJackOLantern";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> GreetLines = "JacquelineGreetLines";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> TreatLines = "JacquelineTreatLines";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> TrickLines = "JacquelineTrickLines";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> NoCandyLines = "JacquelineNoCandyLines";

    /// <summary>
    /// Minimum time between ambient idle barks.
    /// </summary>
    [DataField]
    public TimeSpan BarkCooldown = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan NextBarkTime;

    /// <summary>
    /// Minimum time between trades, so one player can't farm her for infinite rewards.
    /// </summary>
    [DataField]
    public TimeSpan TradeCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan NextTradeTime;

    /// <summary>
    /// How often she teleports to a random living crew member instead of just wandering there.
    /// </summary>
    [DataField]
    public TimeSpan TeleportCooldown = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan NextTeleportTime;

    /// <summary>
    /// How far from the chosen crew member she lands.
    /// </summary>
    [DataField]
    public float TeleportMinRange = 1.5f;

    [DataField]
    public float TeleportMaxRange = 3.5f;
}
