using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._ClawCommand.BloodCult;

/// <summary>
/// Every cult menu. The key tells the server which menu a selection came from.
/// </summary>
[Serializable, NetSerializable]
public enum BloodCultUiKey : byte
{
    /// <summary>Blood magic: prepare or dispel spells. Radial, on the cultist.</summary>
    Spells,
    /// <summary>Teleport spell destination. List, on the cultist.</summary>
    TeleportSpell,
    /// <summary>Construct form. Radial, on a construct shell.</summary>
    Shell,
    /// <summary>Altar, forge and archives. Radial.</summary>
    Factory,
    /// <summary>Runed metal structures. Radial, built client-side.</summary>
    Construction,
    /// <summary>Advanced blood rites. Radial, on the rites aura.</summary>
    Rites,
    /// <summary>Rune scribing. Radial, on the ritual dagger.</summary>
    RuneDrawer,
    /// <summary>Teleport rune destination. List, on the rune.</summary>
    RuneTeleport,
    /// <summary>Cultist to summon. List, on the summoning rune.</summary>
    Summon,
    /// <summary>Cultist to send an item to. List, on the void torch.</summary>
    VoidTorch,
    /// <summary>Teleport rune name. Text entry, on the rune.</summary>
    RuneName,
}

[Serializable, NetSerializable]
public sealed class BloodCultRadialEntry
{
    public string Id;
    public string? Name;
    public EntProtoId? Prototype;
    public SpriteSpecifier? Icon;

    public BloodCultRadialEntry(string id, string? name = null, EntProtoId? prototype = null, SpriteSpecifier? icon = null)
    {
        Id = id;
        Name = name;
        Prototype = prototype;
        Icon = icon;
    }
}

[Serializable, NetSerializable]
public sealed class BloodCultRadialState(List<BloodCultRadialEntry> entries) : BoundUserInterfaceState
{
    public readonly List<BloodCultRadialEntry> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class BloodCultListEntry(string id, string name)
{
    public readonly string Id = id;
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class BloodCultListState(List<BloodCultListEntry> entries) : BoundUserInterfaceState
{
    public readonly List<BloodCultListEntry> Entries = entries;
}

/// <summary>
/// A radial or list option was picked.
/// </summary>
[Serializable, NetSerializable]
public sealed class BloodCultOptionSelectedMessage(string id) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
}

[Serializable, NetSerializable]
public sealed class BloodCultNameSelectedMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}

#region Visuals

[Serializable, NetSerializable]
public enum SoulShardVisualState : byte
{
    HasMind,
    Blessed,
    Sprite,
    Glow,
}

[Serializable, NetSerializable]
public enum ConstructVisualsState : byte
{
    Transforming,
    Sprite,
    Glow,
}

/// <summary>
/// Generic on/off state for cult items and structures (charged, recharged, lit).
/// </summary>
[Serializable, NetSerializable]
public enum GenericCultVisuals : byte
{
    State,
    Layer,
}

[Serializable, NetSerializable]
public enum PylonVisuals : byte
{
    Activated,
    Layer,
}

[Serializable, NetSerializable]
public enum ApocalypseRuneVisuals : byte
{
    Used,
    Layer,
}

[Serializable, NetSerializable]
public enum RendingRuneVisuals : byte
{
    Active,
    Layer,
}

[Serializable, NetSerializable]
public enum BloodCultHaloKey : byte
{
    Key,
}

#endregion
