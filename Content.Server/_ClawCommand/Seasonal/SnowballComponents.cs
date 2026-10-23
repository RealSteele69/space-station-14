using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Seasonal;

/// <summary>
/// Lets a tool scoop snowballs out of snowy floor tiles.
/// </summary>
[RegisterComponent, Access(typeof(SnowballSystem))]
public sealed partial class SnowScoopComponent : Component
{
    [DataField]
    public List<ProtoId<ContentTileDefinition>> Tiles = new() { "FloorSnow", "FloorAstroSnow", "PlatingSnow" };

    [DataField]
    public EntProtoId Spawn = "Snowball";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1.5);
}

/// <summary>
/// A loose snowball that can be packed into a harder one in your hand.
/// </summary>
[RegisterComponent, Access(typeof(SnowballSystem))]
public sealed partial class PackableSnowballComponent : Component
{
    [DataField]
    public EntProtoId PackedInto = "SnowballPacked";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
}
