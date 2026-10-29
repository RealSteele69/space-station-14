// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components.Mobs;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Timing;

namespace Content.Shared._GoobStation.Wraith.Systems.Mobs;

public sealed partial class CloakSystem : EntitySystem
{
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CloakComponent, RushdownEvent>(OnRushdown);
        SubscribeLocalEvent<CloakComponent, CloakEvent>(OnCloak);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CloakComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive)
                continue;

            if (_timing.CurTime >= comp.EndTime)
            {
                RemCompDeferred<StealthComponent>(uid);
                _popup.PopupClient(Loc.GetString("wraith-voidhound-cloak-reappear"), uid, uid);
                comp.IsActive = false;
                Dirty(uid, comp);
            }
        }
    }

    private void OnCloak(Entity<CloakComponent> ent, ref CloakEvent args)
    {
        var stealth = EnsureComp<StealthComponent>(ent.Owner);
        _stealth.SetVisibility(ent.Owner, 0.3f, stealth);

        _popup.PopupClient(Loc.GetString("wraith-voidhound-cloak-slip"), ent.Owner, ent.Owner);
        ent.Comp.IsActive = true;
        ent.Comp.EndTime = _timing.CurTime + ent.Comp.CloakDuration;
        Dirty(ent);

        args.Handled = true;
    }

    private void OnRushdown(Entity<CloakComponent> ent, ref RushdownEvent args)
    {
        if (!ent.Comp.IsActive)
            return;

        RemComp<StealthComponent>(ent.Owner);

        ent.Comp.IsActive = false;
        Dirty(ent);
    }
}
