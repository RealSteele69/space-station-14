using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Shared._ClawCommand.AACTablet;
using Content.Shared._ClawCommand.AACTablet.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.AACTablet;

public sealed partial class AACTabletSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;

    private readonly List<string> _localisedPhrases = [];

    public const int MaxPhrases = 10; // no writing novels

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AACTabletComponent, AACTabletSendPhraseMessage>(OnSendPhrase);
        SubscribeLocalEvent<AACTabletComponent, ActivatableUIOpenAttemptEvent>(OnAACOpenAttempt);

        Subs.BuiEvents<AACTabletComponent>(AACTabletKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBoundUIOpened);
        });
    }

    private HashSet<ProtoId<RadioChannelPrototype>> GetAvailableChannels(EntityUid entity)
    {
        var channels = new HashSet<ProtoId<RadioChannelPrototype>>();

        // Get all the intrinsic radio channels (IPCs, implants)
        if (TryComp(entity, out ActiveRadioComponent? intrinsicRadio))
            channels.UnionWith(intrinsicRadio.Channels);

        // Get the user's headset channels, if any
        if (TryComp(entity, out WearingHeadsetComponent? headset)
            && TryComp(headset.Headset, out ActiveRadioComponent? headsetRadio))
            channels.UnionWith(headsetRadio.Channels);

        return channels;
    }

    private void OnBoundUIOpened(Entity<AACTabletComponent> ent, ref BoundUIOpenedEvent args)
    {
        var state = new AACTabletBuiState(GetAvailableChannels(args.Actor));
        _userInterface.SetUiState(args.Entity, AACTabletKey.Key, state);
    }

    private void OnSendPhrase(Entity<AACTabletComponent> ent, ref AACTabletSendPhraseMessage message)
    {
        if (ent.Comp.NextPhrase > _timing.CurTime || message.PhraseIds.Count > MaxPhrases)
            return;

        var senderName = Identity.Entity(message.Actor, EntityManager);
        var speakerName = Loc.GetString("speech-name-relay",
            ("speaker", Name(ent)),
            ("originalName", senderName));

        _localisedPhrases.Clear();
        foreach (var phraseProto in message.PhraseIds)
        {
            if (_prototype.TryIndex(phraseProto, out var phrase))
            {
                // Ensures each phrase is capitalised to maintain common AAC styling
                _localisedPhrases.Add(_chat.SanitizeMessageCapital(Loc.GetString(phrase.Text)));
            }
        }

        if (_localisedPhrases.Count <= 0)
            return;

        EnsureComp<VoiceOverrideComponent>(ent).NameOverride = speakerName;

        // Only allow the radio channels the user can currently reach.
        var available = GetAvailableChannels(message.Actor);
        EnsureComp(ent, out IntrinsicRadioTransmitterComponent transmitter);
        transmitter.Channels = available;

        var prefix = SanitizePrefix(message.Prefix, available);
        var messageToSend = string.Join(" ", _localisedPhrases);

        _chat.TrySendInGameICMessage(ent,
            prefix + messageToSend,
            InGameICChatType.Speak,
            hideChat: false,
            nameOverride: speakerName);

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"AAC tablet message from {ToPrettyString(message.Actor):user}: {messageToSend}");

        ent.Comp.NextPhrase = _timing.CurTime + ent.Comp.Cooldown;
    }

    /// <summary>
    /// The prefix comes from the client, so only accept local speech or a radio key the user actually has.
    /// </summary>
    private string SanitizePrefix(string prefix, HashSet<ProtoId<RadioChannelPrototype>> available)
    {
        if (prefix.Length != 2 || prefix[0] != SharedChatSystem.RadioChannelPrefix)
            return string.Empty;

        foreach (var channel in available)
        {
            if (_prototype.TryIndex(channel, out var proto) && proto.KeyCode == prefix[1])
                return prefix;
        }

        return string.Empty;
    }

    private void OnAACOpenAttempt(Entity<AACTabletComponent> tablet, ref ActivatableUIOpenAttemptEvent args)
    {
        var ev = new AACTabletOpenAttemptEvent(tablet);
        RaiseLocalEvent(args.User, ref ev);

        if (!ev.Cancelled)
            return;

        args.Cancel();
        if (ev.FailReason == null || args.Silent)
            return;

        var message = Loc.GetString(ev.FailReason);
        _popup.PopupEntity(message, args.User, args.User, PopupType.MediumCaution);
    }
}
