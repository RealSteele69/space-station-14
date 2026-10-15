using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Metabolism;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;

namespace Content.Server._DV.Feroxi;

public sealed partial class FeroxiDehydrateSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SatiationSystem _satiation = default!; // Claw Command - Thirst component retired for Satiation

    // Claw Command - the old ThirstThreshold.Parched, now the matching key on the thirst satiation prototype.
    private static readonly SatiationValue ParchedThreshold = "Parched";

    public override void Update(float frameTime)
    {
        // Claw Command - was a query on the retired ThirstComponent's CurrentThirstThreshold.
        var query = EntityQueryEnumerator<FeroxiDehydrateComponent, SatiationComponent>();

        while (query.MoveNext(out var uid, out var feroxiDehydrate, out var satiation))
        {
            // Under the Parched threshold. False (hydrated) when the entity has no thirst satiation at all.
            var shouldBeDehydrated = _satiation.IsValueInRange((uid, satiation), SatiationSystem.Thirst, below: ParchedThreshold);

            if (feroxiDehydrate.Dehydrated != shouldBeDehydrated)
            {
                UpdateDehydrationStatus((uid, feroxiDehydrate), shouldBeDehydrated);
            }
        }
    }

    /// <summary>
    /// Checks and changes the lungs when meeting the threshold for a swap of metabolizer
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="shouldBeDehydrated"></param>
    private void UpdateDehydrationStatus(Entity<FeroxiDehydrateComponent> ent, bool shouldBeDehydrated)
    {
        ent.Comp.Dehydrated = shouldBeDehydrated;

        _body.TryGetOrgansWithComponent<LungComponent>(ent.Owner, out var organs);
        foreach (var entity in organs)
        {
            if (!TryComp<MetabolizerComponent>(entity, out var metabolizer) || metabolizer.MetabolizerTypes == null)
            {
                continue;
            }
            //Changing the metabolizer to the appropriate value based
            var newMetabolizer = shouldBeDehydrated ? ent.Comp.DehydratedMetabolizer : ent.Comp.HydratedMetabolizer;
            metabolizer.MetabolizerTypes!.Clear();
            metabolizer.MetabolizerTypes.Add(newMetabolizer);
        }
    }
}
