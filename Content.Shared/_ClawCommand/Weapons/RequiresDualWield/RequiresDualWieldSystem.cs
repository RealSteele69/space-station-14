// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.Weapons.RequiresDualWield;

public sealed partial class RequiresDualWieldSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RequiresDualWieldComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RequiresDualWieldComponent, ShotAttemptedEvent>(OnShootAttempt);
    }

    private void OnShootAttempt(Entity<RequiresDualWieldComponent> ent, ref ShotAttemptedEvent args)
    {
        foreach (var held in _hands.EnumerateHeld(args.User))
        {
            if (held != ent.Owner && _whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, held))
                return;
        }

        args.Cancel();

        var time = _timing.CurTime;
        if (time <= ent.Comp.LastPopup + ent.Comp.PopupCooldown)
            return;

        ent.Comp.LastPopup = time;
        _popup.PopupClient(Loc.GetString("dual-wield-component-requires", ("item", ent.Owner)), ent, args.User);
    }

    private void OnExamine(Entity<RequiresDualWieldComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.WieldRequiresExamineMessage is { } message)
            args.PushText(Loc.GetString(message));
    }
}
