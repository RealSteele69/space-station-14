using Content.Shared._ClawCommand.InteractionVerbs;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server._ClawCommand.InteractionVerbs.Actions;

[Serializable]
public sealed partial class ModifyStatusEffectAction : InteractionAction
{
    /// <summary>
    ///     The status effect entity prototype to modify, e.g. StatusEffectStunned.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Effect;

    /// <summary>
    ///     Amount of time added by this action. Can be negative, but then <see cref="EnsureEffect"/> should be false.
    /// </summary>
    [DataField]
    public TimeSpan TimeAdded = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     If true, the action will ensure that the system already has the status effect when removing time,
    ///     or will ensure the entity gets the status effect when adding it.
    /// </summary>
    [DataField]
    public bool EnsureEffect = true;

    public override bool CanPerform(InteractionArgs ctx, InteractionVerbPrototype proto, bool isBefore, VerbDependencies deps)
    {
        var statusSys = deps.EntMan.System<StatusEffectsSystem>();
        return !EnsureEffect || TimeAdded >= TimeSpan.Zero || statusSys.HasStatusEffect(ctx.Target, Effect);
    }

    public override bool Perform(InteractionArgs ctx, InteractionVerbPrototype proto, VerbDependencies deps)
    {
        var statusSys = deps.EntMan.System<StatusEffectsSystem>();

        if (statusSys.HasStatusEffect(ctx.Target, Effect))
            return statusSys.TryAddTime(ctx.Target, Effect, TimeAdded);

        if (EnsureEffect && TimeAdded > TimeSpan.Zero)
            return statusSys.TryAddStatusEffectDuration(ctx.Target, Effect, TimeAdded);

        return false;
    }
}
