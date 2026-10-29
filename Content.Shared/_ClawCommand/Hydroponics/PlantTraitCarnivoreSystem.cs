using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.Shared._ClawCommand.Hydroponics;

/// <inheritdoc cref="PlantTraitCarnivoreComponent"/>
public sealed partial class PlantTraitCarnivoreSystem : EntitySystem
{
    [Dependency] private PlantTraySystem _plantTray = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    [Dependency] private EntityQuery<PlantTrayComponent> _trayQuery;

    [SubscribeLocalEvent]
    private void OnPlantGrow(Entity<PlantTraitCarnivoreComponent> ent, ref PlantGrowEvent args)
    {
        var trayUid = GetEntity(args.Tray);
        if (!_trayQuery.TryComp(trayUid, out var tray))
            return;

        var trayEnt = (trayUid, tray);
        var fed = FixedPoint2.Zero;

        if (_solutionContainer.ResolveSolution(trayUid, tray.SoilSolutionName, ref tray.SoilSolution, out var solution)
            && solution.Volume > 0)
        {
            fed = _solutionContainer.RemoveReagent(tray.SoilSolution.Value, ent.Comp.FeedReagent, FixedPoint2.New(ent.Comp.FeedAmount));
        }

        if (fed > FixedPoint2.Zero)
            _plantTray.AdjustNutrient(trayEnt, fed.Float() * ent.Comp.NutrientPerFeedUnit);
        else
            _plantTray.AdjustNutrient(trayEnt, -ent.Comp.StarveAmount);
    }
}
