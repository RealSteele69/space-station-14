// Claw Command - upstream removed PressureImmunityComponent; barotrauma immunity is now computed by raising
// RefreshPressureImmunityEvent (answered by the StatusEffectPressureImmunity status effect). The permanent
// SpecialPressureImmunityComponent (heretic knowledge, shadekin ethereal) answers the same event so it actually
// grants immunity, and stays independent of any timed pressure-immunity status effect coming and going.

using Content.Goobstation.Common.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;

namespace Content.Server._ClawCommand.HereticDeps;

public sealed partial class SpecialPressureImmunitySystem : EntitySystem
{
    [Dependency] private BarotraumaSystem _barotrauma = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecialPressureImmunityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SpecialPressureImmunityComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<SpecialPressureImmunityComponent, RefreshPressureImmunityEvent>(OnRefreshPressureImmunity);
    }

    private void OnStartup(Entity<SpecialPressureImmunityComponent> ent, ref ComponentStartup args)
    {
        _barotrauma.RefreshPressureImmunity(ent);
    }

    private void OnRemove(Entity<SpecialPressureImmunityComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // The component is still attached while ComponentRemove is raised; OnRefreshPressureImmunity ignores it
        // once it is shutting down, so this refresh falls back to whatever else (status effects) grants immunity.
        _barotrauma.RefreshPressureImmunity(ent);
    }

    private void OnRefreshPressureImmunity(Entity<SpecialPressureImmunityComponent> ent, ref RefreshPressureImmunityEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        args.IsImmune = true;
    }
}
