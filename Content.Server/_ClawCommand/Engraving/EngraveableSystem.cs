using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.Engraving;

public sealed partial class EngraveableSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private QuickDialogSystem _dialog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EngraveableComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EngraveableComponent, GetVerbsEvent<ActivationVerb>>(AddEngraveVerb);
    }

    private void OnExamined(Entity<EngraveableComponent> ent, ref ExaminedEvent args)
    {
        var msg = new FormattedMessage();
        var engraved = ent.Comp.EngravedMessage != string.Empty;

        msg.AddMarkupOrThrow(Loc.GetString(engraved ? ent.Comp.HasEngravingText : ent.Comp.NoEngravingText,
            ("item", ent.Owner)));

        // The engraving is player-written text, so it is shown verbatim rather than parsed as markup.
        if (engraved)
            msg.AddText(ent.Comp.EngravedMessage);

        args.PushMessage(msg, 1);
    }

    private void AddEngraveVerb(Entity<EngraveableComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        // First check if it's already been engraved. If it has, don't let them do it again.
        if (ent.Comp.EngravedMessage != string.Empty)
            return;

        // We need an actor to give the verb.
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        // Make sure ghosts can't engrave stuff.
        if (!args.CanInteract)
            return;

        var session = actor.PlayerSession;
        var engraveVerb = new ActivationVerb
        {
            Text = Loc.GetString("engraving-verb-engrave"),
            Act = () =>
            {
                _dialog.OpenDialog(session,
                    Loc.GetString("engraving-verb-engrave"),
                    Loc.GetString("engraving-popup-ui-message"),
                    (string message) =>
                    {
                        // If either the actor or comp have magically vanished
                        if (session.AttachedEntity is not { } user || !TryComp<EngraveableComponent>(ent, out var comp))
                            return;

                        // Someone else may have engraved it while the dialog was open.
                        if (comp.EngravedMessage != string.Empty)
                            return;

                        message = message.Trim();
                        if (message.Length == 0)
                            return;

                        if (message.Length > comp.MaxLength)
                            message = message[..comp.MaxLength];

                        comp.EngravedMessage = message;
                        _popup.PopupEntity(Loc.GetString(comp.EngraveSuccessMessage, ("item", ent.Owner)),
                            user,
                            user,
                            PopupType.Medium);
                        _adminLogger.Add(LogType.Action,
                            LogImpact.Low,
                            $"{ToPrettyString(user):player} engraved {ToPrettyString(ent.Owner):target} with message: {message}");
                    });
            },
            Impact = LogImpact.Low,
        };
        args.Verbs.Add(engraveVerb);
    }
}
