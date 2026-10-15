using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Mood;

/// <summary>
/// CLAW COMMAND - applies hunger/thirst moodlets from <see cref="SatiationMoodComponent"/>. Replaces the moodlet hooks
/// that lived in the old HungerSystem/ThirstSystem, which upstream retired in favour of the Satiation system.
/// </summary>
public sealed partial class SatiationMoodSystem :
    BaseSatiationEffectSystem<SatiationMoodComponent, ProtoId<MoodEffectPrototype>?>
{
    [Dependency] private SharedMoodSystem _mood = default!;

    protected override Dictionary<ProtoId<SatiationTypePrototype>, SatiationThresholds<ProtoId<MoodEffectPrototype>?>>
        GetThresholds(SatiationMoodComponent comp) => comp.Satiations;

    protected override ProtoId<MoodEffectPrototype>? DefaultValue() => null;

    protected override void AfterSatiationUpdate(Entity<SatiationMoodComponent> entity)
    {
        foreach (var (type, thresholds) in entity.Comp.Satiations)
        {
            if (thresholds.Current is not { } moodlet)
            {
                if (entity.Comp.Applied.Remove(type, out var previous))
                    _mood.RemoveMoodlet(entity, previous);

                continue;
            }

            if (entity.Comp.Applied.TryGetValue(type, out var applied) && applied == moodlet)
                continue;

            entity.Comp.Applied[type] = moodlet;
            _mood.AddMoodlet(entity, moodlet);
        }
    }
}
