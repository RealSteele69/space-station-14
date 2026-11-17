// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Religion.Nullrod;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared.Timing.Systems;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Religion.OnPray;

public sealed partial class TimedToggleOnPraySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private UseDelaySystem _delay = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedToggleOnPrayComponent, AlternatePrayEvent>(OnPray);
    }

    private void OnPray(Entity<TimedToggleOnPrayComponent> ent, ref AlternatePrayEvent args)
    {
        if (ent.Comp.EndTime != null || _toggle.IsActivated(ent.Owner))
            return;

        if (_delay.IsDelayed(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("timed-toggle-on-pray-cooldown", ("item", ent.Owner)), args.User, args.User);
            return;
        }

        if (!_toggle.TryActivate(ent.Owner, args.User, predicted: false))
            return;

        _delay.TryResetDelay(ent.Owner);
        ent.Comp.EndTime = _timing.CurTime + ent.Comp.Duration;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TimedToggleOnPrayComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.EndTime is not { } end || _timing.CurTime < end)
                continue;

            comp.EndTime = null;
            _toggle.TryDeactivate(uid, predicted: false);
        }
    }
}
