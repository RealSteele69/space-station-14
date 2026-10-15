using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Mood;

/// <summary>
/// CLAW COMMAND - feeds an entity's satiations (hunger, thirst) into its mood. Each satiation threshold maps to the
/// moodlet applied while the entity sits in that threshold; moodlets of one satiation share a mood category, so
/// entering a new threshold replaces the previous moodlet.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(SatiationMoodSystem))]
public sealed partial class SatiationMoodComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<ProtoId<MoodEffectPrototype>?>> Satiations = [];

    /// <summary>
    /// The moodlet last applied per satiation, so re-raised satiation updates don't re-apply the same moodlet.
    /// </summary>
    [ViewVariables]
    public Dictionary<ProtoId<SatiationTypePrototype>, ProtoId<MoodEffectPrototype>> Applied = [];
}
