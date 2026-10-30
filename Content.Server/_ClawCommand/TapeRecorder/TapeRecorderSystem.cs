using System.Text;
using Content.Server.Chat.Systems;
using Content.Server.Hands.Systems;
using Content.Shared._ClawCommand.TapeRecorder;
using Content.Shared._ClawCommand.TapeRecorder.Components;
using Content.Shared._ClawCommand.TapeRecorder.Systems;
using Content.Shared.Chat;
using Content.Shared.Paper;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;

namespace Content.Server._ClawCommand.TapeRecorder;

public sealed partial class TapeRecorderSystem : SharedTapeRecorderSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private PaperSystem _paper = default!;

    private readonly List<TapeCassetteRecordedMessage> _toReplay = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TapeRecorderComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<TapeRecorderComponent, PrintTapeRecorderMessage>(OnPrintMessage);
    }

    /// <summary>
    /// Given a time range, play all messages on a tape within said range, [start, end).
    /// Split into this system as shared does not have ChatSystem access
    /// </summary>
    protected override void ReplayMessagesInSegment(Entity<TapeRecorderComponent> ent, TapeCassetteComponent tape, float segmentStart, float segmentEnd)
    {
        _toReplay.Clear();
        foreach (var message in tape.RecordedData)
        {
            if (message.Timestamp < segmentStart || message.Timestamp >= segmentEnd)
                continue;

            _toReplay.Add(message);
        }

        if (_toReplay.Count == 0)
            return;

        EnsureComp<SpeechComponent>(ent);

        // The recorder may be something that also talks on its own (e.g. a pAI), so the voice override is only
        // applied for the playback and restored afterwards.
        var hadOverride = TryComp<VoiceOverrideComponent>(ent, out var voice);
        voice ??= AddComp<VoiceOverrideComponent>(ent);
        var (oldName, oldVerb, oldEnabled) = (voice.NameOverride, voice.SpeechVerbOverride, voice.Enabled);

        foreach (var message in _toReplay)
        {
            // Change the voice to match the speaker
            voice.Enabled = true;
            voice.NameOverride = message.Name ?? Loc.GetString(ent.Comp.DefaultName);
            voice.SpeechVerbOverride = message.Verb ?? SharedChatSystem.DefaultSpeechVerb;

            // Play the message. Recorded text is never treated as a radio prefix.
            _chat.TrySendInGameICMessage(ent, message.Message, InGameICChatType.Speak, false, checkRadioPrefix: false);
        }

        _toReplay.Clear();

        if (!hadOverride)
        {
            RemComp<VoiceOverrideComponent>(ent);
            return;
        }

        voice.NameOverride = oldName;
        voice.SpeechVerbOverride = oldVerb;
        voice.Enabled = oldEnabled;
    }

    /// <summary>
    /// Whenever someone speaks within listening range, record it to tape
    /// </summary>
    private void OnListen(Entity<TapeRecorderComponent> ent, ref ListenEvent args)
    {
        // mode should never be set when it isn't active but whatever
        if (ent.Comp.Mode != TapeRecorderMode.Recording || !HasComp<ActiveTapeRecorderComponent>(ent))
            return;

        // A recorder never speaks while recording, so anything it hears from itself here is its own owner talking
        // (e.g. a pAI recording its own words); that is fine to record.
        if (!TryGetTapeCassette(ent, out var cassette))
            return;

        // Handle someone using a voice changer
        var nameEv = new TransformSpeakerNameEvent(args.Source, Name(args.Source));
        RaiseLocalEvent(args.Source, nameEv);

        // Add a new entry to the tape
        var verb = nameEv.SpeechVerb ?? _chat.GetSpeechVerb(args.Source, args.Message).ID;
        var name = nameEv.VoiceName;
        cassette.Comp.Buffer.Add(new TapeCassetteRecordedMessage(cassette.Comp.CurrentPosition, name, verb, args.Message));
    }

    private void OnPrintMessage(Entity<TapeRecorderComponent> ent, ref PrintTapeRecorderMessage args)
    {
        var comp = ent.Comp;

        if (!comp.CanPrint || comp.CooldownEndTime > Timing.CurTime)
            return;

        if (!TryGetTapeCassette(ent, out var cassette))
            return;

        var text = new StringBuilder();
        var paper = SpawnNextToOrDrop(comp.PaperPrototype, ent);

        // Sorting list by time for overwrite order
        var data = cassette.Comp.RecordedData;
        data.Sort((x, y) => x.Timestamp.CompareTo(y.Timestamp));

        // Give the paper to the player who printed it, if possible
        var player = args.Actor;
        if (Exists(player))
            _hands.PickupOrDrop(player, paper, checkActionBlocker: false);

        if (!TryComp<PaperComponent>(paper, out var paperComp))
            return;

        Audio.PlayPvs(comp.PrintSound, ent);

        text.AppendLine(Loc.GetString("tape-recorder-print-start-text"));
        text.AppendLine();
        foreach (var message in cassette.Comp.RecordedData)
        {
            var name = message.Name ?? Loc.GetString(ent.Comp.DefaultName);
            var time = TimeSpan.FromSeconds((double) message.Timestamp);

            text.AppendLine(Loc.GetString("tape-recorder-print-message-text",
                ("time", time.ToString(@"hh\:mm\:ss")),
                ("source", name),
                ("message", message.Message)));
        }
        text.AppendLine();
        text.Append(Loc.GetString("tape-recorder-print-end-text"));

        _paper.SetContent((paper, paperComp), text.ToString());

        comp.CooldownEndTime = Timing.CurTime + comp.PrintCooldown;
    }
}
