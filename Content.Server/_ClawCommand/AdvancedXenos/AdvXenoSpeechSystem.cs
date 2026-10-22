using Content.Server.Radio.EntitySystems;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - xenomorphs speak the xenomorph tongue out loud (anyone else just hears hissing)
/// and talk plainly over the hivemind (:a), which only hive-node holders receive.
/// A plain accent would garble the hivemind too, so the clean words are kept back and sent over it here.
/// </summary>
public sealed partial class AdvXenoSpeechSystem : EntitySystem
{
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private ReplacementAccentSystem _accent = default!;

    private const string Accent = "xeno";
    private static readonly ProtoId<RadioChannelPrototype> Hivemind = "AdvXenoHivemind";

    private readonly Dictionary<EntityUid, string> _plain = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoHiveComponent, TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<AdvXenoHiveComponent, EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<AdvXenoHiveComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<AdvXenoHiveComponent> ent, ref ComponentShutdown args)
    {
        _plain.Remove(ent);
    }

    private void OnTransformSpeech(Entity<AdvXenoHiveComponent> ent, ref TransformSpeechEvent args)
    {
        _plain[ent] = args.Message;
        args.Message = _accent.ApplyReplacements(args.Message, Accent, ent);
    }

    private void OnSpoke(Entity<AdvXenoHiveComponent> ent, ref EntitySpokeEvent args)
    {
        if (!_plain.Remove(ent, out var plain) || args.Channel is not { } channel || channel.ID != Hivemind)
            return;

        _radio.SendRadioMessage(ent, plain, channel, ent);
        args.Channel = null;
    }
}
