using Content.Shared.Botany.Traits.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Hydroponics;

/// <summary>
/// A plant trait for carnivorous plants that don't feed normally off tray nutrients.
/// Each growth tick, the tray's soil solution is checked for the feed reagent; if enough
/// is present it's consumed and converted into a large nutrient boost, otherwise the plant
/// starves and loses nutrition instead of drawing it from the soil like a normal plant.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlantTraitCarnivoreComponent : PlantTraitsComponent
{
    /// <summary>
    /// The reagent this plant needs to be fed with. Defaults to the reagent raw meat
    /// yields when ground, so feeding it means grinding meat and watering the tray with it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string FeedReagent = "UncookedAnimalProteins";

    /// <summary>
    /// How much feed reagent is consumed from the soil per growth tick, if available.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FeedAmount = 5f;

    /// <summary>
    /// Nutrient gained per unit of feed reagent consumed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float NutrientPerFeedUnit = 4f;

    /// <summary>
    /// Nutrient lost per growth tick when the plant isn't fed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StarveAmount = 2f;

    /// <inheritdoc/>
    public override LocId? TraitState { get; set; } = "mutation-plant-description-carnivore";

    /// <inheritdoc/>
    public override LocId? TraitName { get; set; } = "mutation-plant-noun-carnivore";
}
