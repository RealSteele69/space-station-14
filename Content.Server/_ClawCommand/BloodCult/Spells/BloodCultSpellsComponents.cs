using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.BloodCult.Spells;

/// <summary>
/// Lets a cultist prepare blood magic. Spells are one-use (or few-use) actions that vanish once spent.
/// </summary>
[RegisterComponent]
public sealed partial class BloodCultSpellsHolderComponent : Component
{
    /// <summary>
    /// The spells a cultist can prepare.
    /// </summary>
    [DataField]
    public List<EntProtoId> AvailableSpells = new()
    {
        "ActionBloodCultStun",
        "ActionBloodCultTeleport",
        "ActionBloodCultEmp",
        "ActionBloodCultShadowShackles",
        "ActionBloodCultTwistedConstruction",
        "ActionBloodCultSummonCombatEquipment",
        "ActionBloodCultSummonRitualDagger",
        "ActionBloodCultBloodRites",
    };

    [DataField]
    public int DefaultMaxSpells = 1;

    [DataField]
    public TimeSpan SpellCreationTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Preparing magic without an empowering rune costs blood.
    /// </summary>
    [DataField]
    public DamageSpecifier SpellCreationDamage = new();

    [ViewVariables]
    public int ExtraSpells;

    public int MaxSpells => DefaultMaxSpells + ExtraSpells;

    [ViewVariables]
    public List<EntityUid> SelectedSpells = new();

    [ViewVariables]
    public EntityUid? PrepareAction;

    /// <summary>
    /// The cultist the teleport spell was cast on, while the caster picks a destination.
    /// </summary>
    [ViewVariables]
    public EntityUid? PendingTeleportTarget;

    [ViewVariables]
    public TimeSpan PendingTeleportDuration;
}

/// <summary>
/// Marks a blood spell action. Spent spells are removed from the caster.
/// </summary>
[RegisterComponent]
public sealed partial class BloodCultSpellComponent : Component
{
    /// <summary>
    /// Whether the caster has to be able to speak to cast this.
    /// </summary>
    [DataField]
    public bool RequiresSpeech = true;
}

/// <summary>
/// Granted by the empowering rune: cheaper, faster magic and more prepared spells.
/// Lasts while the cultist stands near cult floor, and a short while after leaving it.
/// </summary>
[RegisterComponent]
public sealed partial class BloodCultEmpoweredComponent : Component
{
    /// <summary>
    /// Multiplies the damage taken from scribing runes and preparing spells.
    /// </summary>
    [DataField]
    public float RuneDamageMultiplier = 0.5f;

    /// <summary>
    /// Multiplies the time it takes to scribe runes and prepare spells.
    /// </summary>
    [DataField]
    public float RuneTimeMultiplier = 0.5f;

    [DataField]
    public int ExtraSpells = 3;

    [DataField]
    public TimeSpan DefaultTime = TimeSpan.FromSeconds(20);

    [DataField]
    public float NearbyCultTileRadius = 1f;

    [DataField]
    public ProtoId<ContentTileDefinition> CultTile = "CultFloor";

    [DataField]
    public ProtoId<AlertPrototype> EmpoweredAlert = "CultEmpowered";

    [ViewVariables]
    public TimeSpan TimeRemaining;
}
