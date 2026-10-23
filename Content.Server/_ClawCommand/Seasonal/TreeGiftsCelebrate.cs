using Content.Server.Holiday;
using Content.Server.Holiday.Interfaces;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._ClawCommand.Seasonal;

/// <summary>
/// Holiday celebration: when the round starts, piles a few presents onto the free floor around
/// every station christmas tree.
/// </summary>
[DataDefinition]
public sealed partial class TreeGiftsCelebrate : IHolidayCelebrate
{
    [DataField(required: true)]
    public List<EntProtoId> Trees = new();

    [DataField]
    public EntProtoId Gift = "PresentRandom";

    /// <summary>
    /// Presents placed on each free tile next to a tree.
    /// </summary>
    [DataField]
    public int MinPerTile;

    [DataField]
    public int MaxPerTile = 2;

    public void Celebrate(HolidayPrototype holiday)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var mapSys = entMan.System<SharedMapSystem>();
        var turf = entMan.System<TurfSystem>();

        var trees = new HashSet<string>();
        foreach (var tree in Trees)
            trees.Add(tree);

        var spots = new List<EntityCoordinates>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out _, out var meta, out var xform))
        {
            if (meta.EntityPrototype is not { } proto || !trees.Contains(proto.ID))
                continue;

            if (xform.GridUid is not { } gridUid
                || !entMan.HasComponent<StationMemberComponent>(gridUid)
                || !entMan.TryGetComponent<MapGridComponent>(gridUid, out var grid))
                continue;

            var center = mapSys.TileIndicesFor(gridUid, grid, xform.Coordinates);
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    var indices = center + new Vector2i(x, y);
                    if (!mapSys.TryGetTileRef(gridUid, grid, indices, out var tile)
                        || turf.IsSpace(tile)
                        || turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                        continue;

                    spots.Add(mapSys.GridTileToLocal(gridUid, grid, indices));
                }
            }
        }

        foreach (var spot in spots)
        {
            var count = random.Next(MinPerTile, MaxPerTile + 1);
            for (var i = 0; i < count; i++)
                entMan.SpawnEntity(Gift, spot);
        }
    }
}
