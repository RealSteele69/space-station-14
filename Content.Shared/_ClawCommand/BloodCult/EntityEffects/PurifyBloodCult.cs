using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.BloodCult.EntityEffects;

/// <summary>
/// Holy water slowly purges the blood pact from a cultist.
/// </summary>
public sealed partial class PurifyBloodCult : EntityEffectBase<PurifyBloodCult>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-purify-blood-cult", ("chance", Probability));
}
