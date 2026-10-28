using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server.NPC.HTN.Preconditions;

/// <summary>
/// Is the entity at the given key stunned/knocked down (stamina critical) or otherwise incapacitated.
/// Unlike <see cref="HasStatusEffectPrecondition"/> this checks an arbitrary blackboard target rather
/// than the NPC itself - used by security bots to know when a perp is down and ready to be cuffed.
/// </summary>
public sealed partial class TargetIncapacitatedPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;
    private MobStateSystem _mobState = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _mobState = sysManager.GetEntitySystem<MobStateSystem>();
    }

    public override bool IsMet(NPCBlackboard blackboard)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return false;

        if (_entManager.TryGetComponent<StaminaComponent>(target, out var stamina) && stamina.Critical)
            return true;

        return _mobState.IsIncapacitated(target);
    }
}
