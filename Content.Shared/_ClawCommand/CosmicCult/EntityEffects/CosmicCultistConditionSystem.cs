using Content.Shared._ClawCommand.CosmicCult.Components;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.CosmicCult.EntityEffects;

/// <summary>
/// Returns true if the entity is a cosmic cultist. Use <c>inverted: true</c> for "not a cultist".
/// </summary>
public sealed partial class CosmicCultistConditionSystem : EntityConditionSystem<TransformComponent, CosmicCultistCondition>
{
    protected override void Condition(Entity<TransformComponent> entity, ref EntityConditionEvent<CosmicCultistCondition> args)
    {
        args.Result = HasComp<CosmicCultComponent>(entity);
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class CosmicCultistCondition : EntityConditionBase<CosmicCultistCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) =>
        Loc.GetString(Inverted ? "reagent-condition-is-not-cultist" : "reagent-condition-is-cultist");
}
