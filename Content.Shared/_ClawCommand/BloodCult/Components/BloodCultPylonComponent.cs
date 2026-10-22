using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.BloodCult.Components;

/// <summary>
/// A cult pylon. Slowly heals nearby cult members and corrupts the floor around it.
/// Shared so the client can keep pylons from being placed next to each other.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodCultPylonComponent : Component
{
    [DataField]
    public bool IsActive = true;

    [DataField]
    public float HealingAuraRange = 5f;

    [DataField]
    public float CorruptionRadius = 5f;

    /// <summary>
    /// No other pylon may be built within this range.
    /// </summary>
    [DataField]
    public float PlacementExclusionRange = 10f;

    [DataField]
    public TimeSpan CorruptionCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan HealingCooldown = TimeSpan.FromSeconds(20);

    [DataField]
    public ProtoId<ContentTileDefinition> CultTile = "CultFloor";

    [DataField]
    public EntProtoId TileCorruptEffect = "CultTileSpawnEffect";

    [DataField]
    public SoundSpecifier BurnHandSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");

    [DataField]
    public SoundSpecifier CorruptTileSound = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/curse.ogg");

    /// <summary>
    /// Healing per damage group, applied evenly across the group's types. Values must be negative.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> Healing = new();

    [DataField]
    public DamageSpecifier DamageOnInteract = new();

    [ViewVariables]
    public TimeSpan NextCorruption;

    [ViewVariables]
    public TimeSpan NextHealing;
}
