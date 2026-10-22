// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.CheatDeath;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Devil.Condemned;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.Devil;

/// <summary>
/// A quick deal: shaking the devil's hand trades your soul for a full heal, one revive and a random downside.
/// </summary>
public sealed partial class DevilSystem
{
    private static readonly SpriteSpecifier HandshakeIcon = new SpriteSpecifier.Rsi(new("_ClawCommand/Devil/Actions/devil.rsi"), "summon-contract");

    private void InitializeHandshakeSystem()
    {
        SubscribeLocalEvent<DevilComponent, GetVerbsEvent<InnateVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PendingHandshakeComponent, GetVerbsEvent<InnateVerb>>(OnGetVerbsPending);
    }

    private void OnGetVerbs(EntityUid uid, DevilComponent comp, GetVerbsEvent<InnateVerb> args)
    {
        // Can't shake your own hand, and you can't shake from a distance
        if (!args.CanAccess
            || !args.CanInteract
            || _state.IsIncapacitated(args.Target)
            || !HasComp<MobStateComponent>(args.Target)
            || HasComp<CondemnedComponent>(args.Target)
            || args.Target == args.User
            || !HasHands(uid) // cant shake if you have no hands
            || !HasHands(args.Target) // or if they have none
            || !_contract.IsUserValid(args.Target, out _))
            return;

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new InnateVerb
        {
            Act = () => OfferHandshake(user, target),
            Text = Loc.GetString("hand-shake-prompt-verb", ("target", target)),
            Icon = HandshakeIcon,
            Priority = 1, // Higher priority than default verbs
        });
    }

    private void OnGetVerbsPending(EntityUid uid, PendingHandshakeComponent comp, GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanAccess
            || !args.CanInteract
            || _state.IsIncapacitated(args.Target)
            || !HasComp<MobStateComponent>(args.Target)
            || args.Target != comp.Offerer)
            return;

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new InnateVerb
        {
            Act = () => HandleHandshake(target, user),
            Text = Loc.GetString("hand-shake-accept-verb", ("target", target)),
            Icon = HandshakeIcon,
            Priority = 1,
        });
    }

    private bool HasHands(EntityUid uid)
    {
        return TryComp<HandsComponent>(uid, out var hands) && hands.Count > 0;
    }

    private void OfferHandshake(EntityUid user, EntityUid target)
    {
        if (HasComp<DevilComponent>(target)
            || HasComp<PendingHandshakeComponent>(target)
            || !_contract.IsUserValid(target, out _))
            return;

        var pending = AddComp<PendingHandshakeComponent>(target);
        pending.Offerer = user;
        pending.ExpiryTime = _timing.CurTime + TimeSpan.FromSeconds(15);

        _popup.PopupEntity(Loc.GetString("handshake-offer-popup", ("user", user)), target, target);
        _popup.PopupEntity(Loc.GetString("handshake-offer-popup-self", ("target", target)), user, user);
    }

    private void HandleHandshake(EntityUid devil, EntityUid target)
    {
        RemComp<PendingHandshakeComponent>(target);

        if (!_contract.TryTransferSouls(devil, target, 1))
        {
            _popup.PopupEntity(Loc.GetString("handshake-fail", ("user", devil)), target, target);
            return;
        }

        _popup.PopupEntity(Loc.GetString("handshake-success", ("user", devil)), target, target);
        _rejuvenate.PerformRejuvenate(target);

        var cheatDeath = EnsureComp<CheatDeathComponent>(target);
        cheatDeath.ReviveAmount = 1;

        _contract.AddRandomNegativeClause(target);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PendingHandshakeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ExpiryTime > _timing.CurTime)
                continue;

            RemCompDeferred(uid, comp);
        }
    }
}
