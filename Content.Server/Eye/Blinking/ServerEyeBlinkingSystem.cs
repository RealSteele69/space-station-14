using Content.Shared.Body;
using Content.Shared.Chat;
using Content.Shared.Eye.Blinking;

namespace Content.Server.Eye.Blinking;

/// <inheritdoc/>
public sealed partial class ServerEyeBlinkingSystem : EyeBlinkingSystem
{
    // TODO: Move all this stuff into Shared once chat is predicted.
    // Claw Command: the blink is purely visual, so it neither checks nor sets Handled. The Blink emote is
    // category General (all flags), and our emote sound sets have a Blink sound, so BodyEmotes/Vocal would
    // otherwise mark it handled first and skip the eyelids (or the eyelids would swallow the sound).
    [SubscribeLocalEvent]
    private void OnEmote(Entity<EyeBlinkingComponent> ent, ref EmoteEvent args)
    {
        TrySendEmoteMessage(ent, args.Emote.ID);
    }

    [SubscribeLocalEvent]
    private void OnEmote(Entity<EyeBlinkingComponent> ent, ref BodyRelayedEvent<EmoteEvent> args)
    {
        TrySendEmoteMessage(ent, args.Args.Emote.ID);
    }

    private bool TrySendEmoteMessage(Entity<EyeBlinkingComponent> ent, string emoteId)
    {
        if (!ent.Comp.BlinkEmoteId.Contains(emoteId))
            return false;

        if (ent.Comp.Status != BlinkStatus.Normal)
            return false;

        var ev = new BlinkEyeEvent(GetNetEntity(ent.Owner));
        RaiseNetworkEvent(ev);
        return true;
    }
}
