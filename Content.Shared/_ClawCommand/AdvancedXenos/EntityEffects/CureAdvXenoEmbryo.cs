using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.AdvancedXenos.EntityEffects;

/// <summary>
/// Cures an implanted alien embryo, provided the host has not started bursting yet.
/// </summary>
public sealed partial class CureAdvXenoEmbryo : EntityEffectBase<CureAdvXenoEmbryo>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-cure-adv-xeno-embryo", ("chance", Probability));
}
