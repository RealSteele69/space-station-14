using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.Silicons.Bots;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators.Specific;

/// <summary>
/// Sits between target selection and the chase in a security bot's combat compound.
/// The first time a given target is picked, this announces the infraction (locally and over
/// the radio) and plays a warning sound, then always finishes immediately - it never blocks
/// the actual chase/attack from happening afterwards.
/// </summary>
public sealed partial class SecurityBotAnnounceTargetOperator : HTNOperator
{
    [Dependency] private IEntityManager _entMan = default!;
    private ChatSystem _chat = default!;
    private SecurityBotSystem _securityBot = default!;
    private SharedAudioSystem _audio = default!;

    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Effects/alert.ogg");

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _chat = sysManager.GetEntitySystem<ChatSystem>();
        _securityBot = sysManager.GetEntitySystem<SecurityBotSystem>();
        _audio = sysManager.GetEntitySystem<SharedAudioSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entMan) || _entMan.Deleted(target))
            return HTNOperatorStatus.Finished;

        if (!_entMan.TryGetComponent<SecurityBotComponent>(owner, out var botComp))
            return HTNOperatorStatus.Finished;

        var bot = (owner, botComp);

        if (!_securityBot.ShouldReacquire(bot, target))
            return HTNOperatorStatus.Finished;

        _securityBot.MarkPerpSeen(bot, target);

        _chat.TrySendInGameICMessage(
            owner,
            Loc.GetString("security-bot-taunt", ("name", Identity.Name(target, _entMan))),
            InGameICChatType.Speak,
            hideChat: false,
            hideLog: true);

        if (_securityBot.ShouldDeclareArrests(bot))
        {
            _chat.TrySendInGameICMessage(
                owner,
                Loc.GetString("security-bot-infraction-alert", ("name", Identity.Name(target, _entMan))),
                InGameICChatType.Speak,
                hideChat: true,
                hideLog: true);
        }

        _audio.PlayPvs(AlertSound, owner, AudioParams.Default.WithVolume(-2f));

        return HTNOperatorStatus.Finished;
    }
}
