using System.Linq;
using System.Numerics;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Alert;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._ClawCommand.BloodCult.Spells;

public sealed partial class BloodCultEmpoweredSystem : EntitySystem
{
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultEmpoweredComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BloodCultEmpoweredComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodCultEmpoweredComponent>();
        while (query.MoveNext(out var uid, out var empowered))
        {
            if (!HasComp<BloodCultistComponent>(uid))
            {
                RemCompDeferred(uid, empowered);
                continue;
            }

            if (AnyCultTilesNearby((uid, empowered)))
            {
                empowered.TimeRemaining = empowered.DefaultTime;
                continue;
            }

            empowered.TimeRemaining -= TimeSpan.FromSeconds(frameTime);
            if (empowered.TimeRemaining <= TimeSpan.Zero)
                RemCompDeferred(uid, empowered);
        }
    }

    private void OnStartup(Entity<BloodCultEmpoweredComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.TimeRemaining = ent.Comp.DefaultTime;
        _alerts.ShowAlert(ent.Owner, ent.Comp.EmpoweredAlert);

        if (TryComp<BloodCultSpellsHolderComponent>(ent, out var spells))
            spells.ExtraSpells += ent.Comp.ExtraSpells;
    }

    private void OnShutdown(Entity<BloodCultEmpoweredComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.EmpoweredAlert);

        if (TryComp<BloodCultSpellsHolderComponent>(ent, out var spells))
            spells.ExtraSpells = Math.Max(0, spells.ExtraSpells - ent.Comp.ExtraSpells);
    }

    private bool AnyCultTilesNearby(Entity<BloodCultEmpoweredComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !_tileDefinition.TryGetDefinition(ent.Comp.CultTile, out var cultTile))
            return false;

        var radius = ent.Comp.NearbyCultTileRadius;
        var pos = xform.Coordinates.Position;
        var box = new Box2(pos - new Vector2(radius, radius), pos + new Vector2(radius, radius));
        return _map.GetLocalTilesIntersecting(gridUid, grid, box).Any(tile => tile.Tile.TypeId == cultTile.TileId);
    }
}
