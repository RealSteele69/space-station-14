using Content.Server.Radio.EntitySystems;
using Content.Shared._ClawCommand.Blob.Components;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server._ClawCommand.Blob;

/// <summary>
/// Blob members talk to each other over the Blobmind radio channel (:l) in plain words,
/// while anything they say out loud is turned into blob gibberish.
/// The observer camera (<see cref="BlobSpeakComponent.RadioOnly"/>) can only talk over the Blobmind.
/// An accent alone would garble the Blobmind too, so the clean words are kept back and sent over it here.
/// </summary>
public sealed partial class BlobSpeakSystem : EntitySystem
{
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private ReplacementAccentSystem _accent = default!;

    private readonly Dictionary<EntityUid, string> _plain = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobSpeakComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BlobSpeakComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BlobSpeakComponent, TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<BlobSpeakComponent, EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<BlobSpeakComponent, GetDefaultRadioChannelEvent>(OnGetDefaultChannel);
        SubscribeLocalEvent<BlobSpeakComponent, TransformSpeakerNameEvent>(OnSpeakerName);
    }

    private void OnStartup(Entity<BlobSpeakComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<IntrinsicRadioReceiverComponent>(ent))
        {
            AddComp<IntrinsicRadioReceiverComponent>(ent);
            ent.Comp.AddedReceiver = true;
        }

        if (!TryComp<ActiveRadioComponent>(ent, out var radio))
        {
            radio = AddComp<ActiveRadioComponent>(ent);
            ent.Comp.AddedActiveRadio = true;
        }

        ent.Comp.AddedChannel = radio.Channels.Add(ent.Comp.Channel);
    }

    private void OnShutdown(Entity<BlobSpeakComponent> ent, ref ComponentShutdown args)
    {
        _plain.Remove(ent);

        if (TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.AddedActiveRadio)
            RemComp<ActiveRadioComponent>(ent);
        else if (ent.Comp.AddedChannel && TryComp<ActiveRadioComponent>(ent, out var radio))
            radio.Channels.Remove(ent.Comp.Channel);

        if (ent.Comp.AddedReceiver)
            RemComp<IntrinsicRadioReceiverComponent>(ent);
    }

    private void OnTransformSpeech(Entity<BlobSpeakComponent> ent, ref TransformSpeechEvent args)
    {
        if (ent.Comp.RadioOnly)
        {
            // Nothing is ever said out loud, every word goes to the Blobmind.
            if (args.Message.Length > 0)
                _radio.SendRadioMessage(ent, args.Message, ent.Comp.Channel, ent);

            args.Message = string.Empty;
            return;
        }

        _plain[ent] = args.Message;
        args.Message = _accent.ApplyReplacements(args.Message, ent.Comp.Accent, ent);
    }

    private void OnSpoke(Entity<BlobSpeakComponent> ent, ref EntitySpokeEvent args)
    {
        if (!_plain.Remove(ent, out var plain) || args.Channel is not { } channel || channel.ID != ent.Comp.Channel)
            return;

        _radio.SendRadioMessage(ent, plain, channel, ent);
        args.Channel = null;
    }

    private void OnGetDefaultChannel(Entity<BlobSpeakComponent> ent, ref GetDefaultRadioChannelEvent args)
    {
        args.Channel = ent.Comp.Channel;
    }

    private void OnSpeakerName(Entity<BlobSpeakComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (ent.Comp.OverrideName)
            args.VoiceName = Loc.GetString(ent.Comp.Name);
    }
}
