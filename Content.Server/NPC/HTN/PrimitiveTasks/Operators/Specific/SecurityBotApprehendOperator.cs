using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Silicons.Bots;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Run once a security bot's target is adjacent and incapacitated: zip-ties them (if configured to)
/// and announces the arrest over the radio, mirroring a real officer declaring a detainment.
/// </summary>
public sealed partial class SecurityBotApprehendOperator : HTNOperator
{
    [Dependency] private IEntityManager _entMan = default!;
    private ChatSystem _chat = default!;
    private SecurityBotSystem _securityBot = default!;
    private SharedCuffableSystem _cuffable = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _chat = sysManager.GetEntitySystem<ChatSystem>();
        _securityBot = sysManager.GetEntitySystem<SecurityBotSystem>();
        _cuffable = sysManager.GetEntitySystem<SharedCuffableSystem>();
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entMan) || _entMan.Deleted(target))
            return HTNOperatorStatus.Failed;

        if (!_entMan.TryGetComponent<SecurityBotComponent>(owner, out var botComp))
            return HTNOperatorStatus.Failed;

        var bot = (owner, botComp);

        // Target got away or died while we were closing in - go back to hunting/patrolling.
        if (!_securityBot.IsValidTarget(bot, target))
        {
            _securityBot.MarkPerpLost(bot);
            return HTNOperatorStatus.Failed;
        }

        if (!_securityBot.ShouldHandcuff(bot))
        {
            // Warning mode: just leave them stunned and move on.
            Announce(bot, target, "security-bot-arrest-warning");
            _securityBot.MarkPerpLost(bot);
            return HTNOperatorStatus.Finished;
        }

        if (_entMan.TryGetComponent<CuffableComponent>(target, out var cuffable) && _cuffable.IsCuffed((target, cuffable)))
        {
            // Someone already cuffed them (or we just did last tick) - done.
            _securityBot.MarkPerpLost(bot);
            return HTNOperatorStatus.Finished;
        }

        // Out of reach (perp got up and moved) or cuffing failed: replan so the bot closes in again
        // instead of waiting on this task forever.
        if (!_securityBot.TryCuff(bot, target))
            return HTNOperatorStatus.Failed;

        Announce(bot, target, "security-bot-arrest-declared");
        _securityBot.MarkPerpLost(bot);
        return HTNOperatorStatus.Finished;
    }

    private void Announce(Entity<SecurityBotComponent> bot, EntityUid target, string locId)
    {
        if (!_securityBot.ShouldDeclareArrests((bot.Owner, bot.Comp)))
            return;

        var message = Loc.GetString(locId, ("name", Identity.Name(target, _entMan)));
        _chat.TrySendInGameICMessage(bot.Owner, message, InGameICChatType.Speak, hideChat: false, hideLog: true);
    }
}
