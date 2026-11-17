// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Weapons.RequiresDualWield;
using Content.Shared.Bible.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Network;

namespace Content.Shared._ClawCommand.Religion.Nullrod;

/// <summary>
/// Lets a chaplain pray at an altar to summon their bound null rod back into their hands.
/// </summary>
public sealed partial class RecallPrayableSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedProjectileSystem _projectile = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RecallPrayableComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerb);
        SubscribeLocalEvent<RecallPrayableComponent, RecallPrayDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerb(Entity<RecallPrayableComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<BibleUserComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("chaplain-recall-verb"),
            Act = () =>
            {
                if (!TryComp<NullrodBearerComponent>(user, out var bearer) || bearer.NullRod == null)
                {
                    _popup.PopupClient(Loc.GetString("chaplain-recall-no-nullrod"), user, user);
                    return;
                }

                var doAfterArgs = new DoAfterArgs(EntityManager, user, ent.Comp.DoAfterDuration, new RecallPrayDoAfterEvent(), ent)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                    BlockDuplicate = true,
                };

                _doAfter.TryStartDoAfter(doAfterArgs);
            },
        });
    }

    private void OnDoAfter(Entity<RecallPrayableComponent> ent, ref RecallPrayDoAfterEvent args)
    {
        // Moving entities between containers is server-only.
        if (_net.IsClient || args.Cancelled || args.Handled || TerminatingOrDeleted(args.User))
            return;

        if (!TryComp<NullrodBearerComponent>(args.User, out var bearer) || bearer.NullRod is not { } rod)
            return;

        args.Handled = true;

        if (TerminatingOrDeleted(rod) || !TryComp<NullrodComponent>(rod, out var nullrod))
        {
            _popup.PopupEntity(Loc.GetString("chaplain-recall-nullrod-gone"), args.User, args.User);
            bearer.NullRod = null;
            Dirty(args.User, bearer);
            return;
        }

        // Dual-wield rods can still fetch their missing twin while one is in hand.
        if (_hands.IsHolding(args.User, rod) && !HasComp<RequiresDualWieldComponent>(rod))
        {
            _popup.PopupEntity(Loc.GetString("chaplain-recall-nullrod-already-in-hand", ("nullrod", rod)), args.User, args.User);
            return;
        }

        if (!_hands.TryGetEmptyHand(args.User, out _))
        {
            _popup.PopupEntity(Loc.GetString("chaplain-recall-hands-full"), args.User, args.User);
            return;
        }

        var recalled = nullrod.RecallType switch
        {
            NullrodRecallType.Normal => _hands.TryPickupAnyHand(args.User, rod),
            NullrodRecallType.Unremoveable => RecallUnremoveable(rod, args.User),
            NullrodRecallType.DualWield => RecallDualWield(rod, args.User),
            NullrodRecallType.Embedded => RecallEmbedded(rod, args.User),
            _ => false,
        };

        if (nullrod.RecallType == NullrodRecallType.None)
            _popup.PopupEntity(Loc.GetString("chaplain-recall-none", ("nullrod", rod)), args.User, args.User);
        else if (recalled)
            _popup.PopupEntity(Loc.GetString("chaplain-recall-nullrod-recalled", ("nullrod", rod)), args.User, args.User);
    }

    private bool RecallUnremoveable(EntityUid rod, EntityUid user)
    {
        // Pull it out of whatever hand it's glued to, then glue it back on.
        var wasUnremoveable = RemComp<UnremoveableComponent>(rod);
        var picked = _hands.TryPickupAnyHand(user, rod);

        if (wasUnremoveable)
            EnsureComp<UnremoveableComponent>(rod);

        return picked;
    }

    private bool RecallDualWield(EntityUid rod, EntityUid user)
    {
        if (!TryComp<RequiresDualWieldComponent>(rod, out var dualWield))
            return false;

        var picked = _hands.IsHolding(user, rod) || _hands.TryPickupAnyHand(user, rod);

        var query = EntityQueryEnumerator<RequiresDualWieldComponent, NullrodComponent>();
        while (query.MoveNext(out var twin, out _, out _))
        {
            if (twin == rod || _hands.IsHolding(user, twin) || !_whitelist.IsWhitelistPass(dualWield.Whitelist, twin))
                continue;

            picked |= _hands.TryPickupAnyHand(user, twin);
            break;
        }

        return picked;
    }

    private bool RecallEmbedded(EntityUid rod, EntityUid user)
    {
        if (TryComp<EmbeddableProjectileComponent>(rod, out var embed) && embed.EmbeddedIntoUid != null)
            _projectile.EmbedDetach(rod, embed);

        return _hands.TryPickupAnyHand(user, rod);
    }
}
