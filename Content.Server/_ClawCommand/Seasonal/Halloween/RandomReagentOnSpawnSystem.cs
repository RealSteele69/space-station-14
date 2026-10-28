using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

public sealed partial class RandomReagentOnSpawnSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomReagentOnSpawnComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<RandomReagentOnSpawnComponent> ent, ref MapInitEvent args)
    {
        var comp = ent.Comp;
        if (comp.Reagents.Count == 0)
            return;

        if (!_solutionContainer.EnsureSolution(ent.Owner, comp.SolutionName, out var solution))
            return;

        var reagent = _random.Pick(comp.Reagents);
        _solutionContainer.TryAddReagent(solution, reagent, comp.Quantity, out _);
    }
}
