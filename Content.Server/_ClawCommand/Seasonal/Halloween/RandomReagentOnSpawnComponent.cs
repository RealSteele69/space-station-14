using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Jacqueline's magic potions/candies: on spawn, fills the entity's solution with one randomly
/// chosen reagent from <see cref="Reagents"/> instead of a fixed effect.
/// </summary>
[RegisterComponent]
public sealed partial class RandomReagentOnSpawnComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<ReagentPrototype>> Reagents = new();

    [DataField]
    public FixedPoint2 Quantity = FixedPoint2.New(15);

    /// <summary>
    /// Must match whatever solution name the item's own Edible/Drink chain actually reads from
    /// (e.g. "food" for anything under FoodBase, "drink" for anything under DrinkBase) - it is
    /// NOT the bare SolutionComponent default of "solution".
    /// </summary>
    [DataField]
    public string SolutionName = "solution";
}
