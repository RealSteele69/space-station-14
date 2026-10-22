// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Shared._ClawCommand.HereticAdapters;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Religion;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._ClawCommand.Devil.Grip;

/// <summary>
/// The devil's grip: a one-use touch that knocks the target down, drains their stamina and garbles their speech.
/// </summary>
[RegisterComponent]
public sealed partial class DevilGripComponent : Component
{
    [DataField]
    public TimeSpan CooldownAfterUse = TimeSpan.FromSeconds(20);

    [DataField]
    public EntityWhitelist Blacklist = new();

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(5f);

    [DataField]
    public float StaminaDamage = 80f;

    [DataField]
    public TimeSpan SpeechTime = TimeSpan.FromSeconds(10f);

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/crack2.ogg");

    [DataField]
    public LocId Invocation = "devil-speech-grip";
}

public sealed partial class DevilGripSystem : EntitySystem
{
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private RatvarianLanguageSystem _language = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private DivineInterventionSystem _divineIntervention = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilGripComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<DevilGripComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target is not { } target
            || target == args.User
            || _whitelist.IsWhitelistPass(ent.Comp.Blacklist, target)
            || !TryComp<DevilComponent>(args.User, out var devilComp))
            return;

        args.Handled = true;

        // A held holy item eats the grip.
        if (!_divineIntervention.ShouldDeny(target) && HasComp<MobStateComponent>(target))
        {
            _stun.KnockdownOrStun(target, ent.Comp.KnockdownTime, true);
            _stamina.TakeStaminaDamage(target, ent.Comp.StaminaDamage);
            _language.DoRatvarian(target, ent.Comp.SpeechTime, true);
        }

        _actions.SetCooldown(devilComp.DevilGrip, ent.Comp.CooldownAfterUse);
        devilComp.DevilGrip = null;

        _audio.PlayPvs(ent.Comp.Sound, args.User);
        _chat.TrySendInGameICMessage(args.User, Loc.GetString(ent.Comp.Invocation), InGameICChatType.Speak, false);
        QueueDel(ent);
    }
}
