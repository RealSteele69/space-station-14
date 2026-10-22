using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._ClawCommand.Body.Preening;

/// <summary>
///     Claw Command - A feathered creature that can be preened (by itself or someone else) for a loose feather,
///     and that sometimes has a feather torn out by a heavy brute hit. Feathers regrow over time.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(PreenableSystem))]
public sealed partial class PreenableComponent : Component
{
    /// <summary>
    ///     The feather that is spawned. It is tinted with the preened creature's skin colour.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId FeatherPrototype;

    /// <summary>
    ///     Damage types that count towards tearing a feather out.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DamageTypePrototype>> ValidDamageTypes = new()
    {
        "Blunt",
        "Piercing",
        "Slash",
    };

    [DataField]
    public LocId SelfPreeningMessage = "preening-popup-self";

    [DataField]
    public LocId GettingPreenedMessage = "preening-popup-self-recipient";

    [DataField]
    public LocId PreeningOtherMessage = "preening-popup-other";

    [DataField]
    public LocId FeatherBloodiedNameString = "feather-bloody-name-modifier";

    [DataField]
    public LocId FeatherBloodiedDescString = "feather-bloody-desc";

    [DataField]
    public LocId PreeningVerbString = "preening-action-verb";

    [DataField]
    public LocId DroppedFeatherString = "preening-feather-dropped-injured";

    [DataField]
    public ProtoId<EmotePrototype> ScreamEmote = "Scream";

    /// <summary>
    ///     How long the preening do-after takes.
    /// </summary>
    [DataField]
    public TimeSpan PreenDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     The minimum amount of damage that must be taken from one attack to have a chance to shed a feather.
    /// </summary>
    [DataField]
    public FixedPoint2 ShedDamageThreshold = 9;

    /// <summary>
    ///     The chance for a feather to be shed on hit, per point of damage taken.
    /// </summary>
    [DataField]
    public float ShedScalingChance = 0.0125f;

    /// <summary>
    ///     Optional extra damage taken while feathers are missing; scales from no effect at
    ///     <see cref="MaximumFeathers"/> to the full modifier at zero feathers.
    /// </summary>
    [DataField]
    public DamageModifierSet? VulnerabilityModifier;

    [DataField, AutoNetworkedField]
    public int MaximumFeathers = 3;

    [DataField, AutoNetworkedField]
    public int CurrentFeathers;

    [DataField]
    public TimeSpan ReplenishDelay = TimeSpan.FromSeconds(150);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? ReplenishTime;
}
