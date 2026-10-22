using Content.Server._ClawCommand.CosmicCult.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;

namespace Content.Server._ClawCommand.CosmicCult.EntitySystems;

public sealed partial class CosmicPressureImmunitySystem : EntitySystem
{
    [Dependency] private BarotraumaSystem _barotrauma = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicPressureImmunityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CosmicPressureImmunityComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CosmicPressureImmunityComponent, RefreshPressureImmunityEvent>(OnRefreshPressureImmunity);
    }

    private void OnStartup(Entity<CosmicPressureImmunityComponent> ent, ref ComponentStartup args)
    {
        _barotrauma.RefreshPressureImmunity(ent);
    }

    private void OnRemove(Entity<CosmicPressureImmunityComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // Still attached during ComponentRemove; the refresh handler ignores it once it is stopping.
        _barotrauma.RefreshPressureImmunity(ent);
    }

    private void OnRefreshPressureImmunity(Entity<CosmicPressureImmunityComponent> ent, ref RefreshPressureImmunityEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        args.IsImmune = true;
    }
}
