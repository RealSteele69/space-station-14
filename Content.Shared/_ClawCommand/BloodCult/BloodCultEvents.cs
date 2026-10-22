using Content.Shared._ClawCommand.BloodCult.Runes;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.BloodCult;

#region Actions

/// <summary>
/// Opens the blood magic menu, where cultists prepare new spells or dispel prepared ones.
/// </summary>
public sealed partial class BloodCultPrepareSpellsEvent : InstantActionEvent;

/// <summary>
/// Base for blood spells that make the caster whisper an incantation.
/// </summary>
public interface IBloodCultIncantation
{
    string? Speech { get; }
}

public sealed partial class BloodCultTeleportEvent : EntityTargetActionEvent, IBloodCultIncantation
{
    [DataField]
    public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public string? Speech { get; private set; }
}

public sealed partial class BloodCultEmpEvent : InstantActionEvent, IBloodCultIncantation
{
    [DataField]
    public float Range = 4f;

    [DataField]
    public float EnergyConsumption = 1000f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    [DataField]
    public string? Speech { get; private set; }
}

public sealed partial class BloodCultTwistedConstructionEvent : EntityTargetActionEvent, IBloodCultIncantation
{
    /// <summary>
    /// What can be twisted, and into what. The first matching recipe wins.
    /// </summary>
    [DataField(required: true)]
    public List<TwistedConstructionRecipe> Recipes = new();

    [DataField]
    public string? Speech { get; private set; }
}

/// <summary>
/// One twisted construction conversion. A target matches if it passes the whitelist,
/// is a stack of <see cref="StackType"/>, or was spawned from one of <see cref="Prototypes"/>.
/// </summary>
[DataDefinition]
public sealed partial class TwistedConstructionRecipe
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public ProtoId<StackPrototype>? StackType;

    [DataField]
    public List<EntProtoId> Prototypes = new();

    [DataField(required: true)]
    public EntProtoId Replacement;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Spawns gear into the caster's hands, or straight into an inventory slot for clothing.
/// </summary>
public sealed partial class BloodCultSummonEquipmentEvent : InstantActionEvent, IBloodCultIncantation
{
    /// <summary>
    /// Slot id to entity. Clothing gets equipped into its slot, everything else goes to a free hand.
    /// </summary>
    [DataField]
    public Dictionary<string, EntProtoId> Prototypes = new();

    /// <summary>
    /// When false, the spell fails (and keeps its charge) if the caster has no free hand.
    /// </summary>
    [DataField]
    public bool Force = true;

    [DataField]
    public string? Speech { get; private set; }
}

public sealed partial class BloodSpearRecalledEvent : InstantActionEvent;

/// <summary>
/// Artificer construction: place a cult tile and/or spawn a structure on the targeted tile.
/// </summary>
public sealed partial class BloodCultPlaceTileEntityEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId? Entity;

    [DataField]
    public ProtoId<ContentTileDefinition>? TileId;

    [DataField]
    public SoundSpecifier? Audio;
}

public sealed partial class BloodCultPhaseShiftEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);
}

#endregion

#region DoAfters

[Serializable, NetSerializable]
public sealed partial class BloodCultCreateSpellDoAfterEvent : SimpleDoAfterEvent
{
    public EntProtoId Spell;

    public BloodCultCreateSpellDoAfterEvent(EntProtoId spell)
    {
        Spell = spell;
    }
}

[Serializable, NetSerializable]
public sealed partial class BloodCultTeleportDoAfterEvent : SimpleDoAfterEvent
{
    public NetEntity Rune;

    public BloodCultTeleportDoAfterEvent(NetEntity rune)
    {
        Rune = rune;
    }
}

[Serializable, NetSerializable]
public sealed partial class BloodCultTwistedConstructionDoAfterEvent : SimpleDoAfterEvent
{
    public EntProtoId Replacement;

    public BloodCultTwistedConstructionDoAfterEvent(EntProtoId replacement)
    {
        Replacement = replacement;
    }
}

[Serializable, NetSerializable]
public sealed partial class BloodRitesExtractDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class BloodCultDrawRuneDoAfterEvent : SimpleDoAfterEvent
{
    public ProtoId<RuneSelectorPrototype> Rune;

    public BloodCultDrawRuneDoAfterEvent(ProtoId<RuneSelectorPrototype> rune)
    {
        Rune = rune;
    }
}

[Serializable, NetSerializable]
public sealed partial class BloodCultEraseRuneDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ApocalypseRuneDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class RendingRuneDoAfterEvent : SimpleDoAfterEvent;

#endregion

/// <summary>
/// Raised on a hand aura when it lands, so the caster whispers its incantation.
/// </summary>
[ByRefEvent]
public readonly record struct BloodCultAuraUsedEvent(EntityUid User);
