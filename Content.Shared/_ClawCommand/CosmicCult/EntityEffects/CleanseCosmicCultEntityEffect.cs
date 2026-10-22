using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.CosmicCult.EntityEffects;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class CleanseCosmicCult : EntityEffectBase<CleanseCosmicCult>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cleanse-cultist", ("chance", Probability));
}