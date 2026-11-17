// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Bible.Components;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._ClawCommand.Religion.Nullrod;

/// <summary>
/// Adds a "Pray to" verb to held holy items that do something when prayed to.
/// </summary>
public sealed partial class AlternatePrayableSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlternatePrayableComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AlternatePrayableComponent, AlternatePrayDoAfterEvent>(OnPrayDoAfter);
    }

    private void OnGetVerbs(Entity<AlternatePrayableComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess
            || !args.CanInteract
            || args.Using is not { } item
            || item != ent.Owner
            || !HasComp<ItemComponent>(item))
            return;

        if (ent.Comp.RequireBibleUser && !HasComp<BibleUserComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => StartPrayDoAfter(user, ent),
            Text = Loc.GetString("alternate-pray-prompt", ("item", item)),
            Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Specific/Chapel/bible.rsi"), "icon"),
            Priority = 30,
        });
    }

    private void StartPrayDoAfter(EntityUid user, Entity<AlternatePrayableComponent> item)
    {
        if (_timing.CurTime > item.Comp.NextPopup)
        {
            var popup = Loc.GetString("alternate-pray-start", ("user", Identity.Name(user, EntityManager)), ("item", item.Owner));
            _popup.PopupPredicted(popup, user, user);

            item.Comp.NextPopup = _timing.CurTime + item.Comp.PopupDelay;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager,
            user,
            item.Comp.PrayDoAfterDuration,
            new AlternatePrayDoAfterEvent(),
            item,
            user,
            item)
        {
            BreakOnDamage = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnPrayDoAfter(Entity<AlternatePrayableComponent> ent, ref AlternatePrayDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(args.User))
            return;

        var ev = new AlternatePrayEvent(args.User);
        RaiseLocalEvent(ent, ref ev);

        args.Repeat = ent.Comp.RepeatPrayer;
    }
}
