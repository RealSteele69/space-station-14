// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source checked Content.Shared.Humanoid.HumanoidAppearanceComponent to detect "is a human",
// which doesn't exist on this fork (renamed HumanoidProfileComponent fork-wide).

using Content.Shared._ClawCommand.HereticAdapters;
using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Popups;

namespace Content.Shared._GoobStation.Wraith.Systems;

public sealed partial class DecaySystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private EmagSystem _emag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DecayComponent, DecayEvent>(OnDecay);
    }

    private void OnDecay(Entity<DecayComponent> ent, ref DecayEvent args)
    {
        if (HasComp<HumanoidProfileComponent>(args.Target))
        {
            _stamina.TakeOvertimeStaminaDamage(args.Target, ent.Comp.StaminaDamageAmount);
            _popup.PopupClient(Loc.GetString("wraith-decay-human-alert"), args.Target, args.Target);
            args.Handled = true;
            return;
        }

        if (_emag.TryEmagEffect(ent.Owner, ent.Owner, args.Target, ent.Comp.Emag))
        {
            args.Handled = true;
            return;
        }

        _popup.PopupClient(Loc.GetString("wraith-decay-nothing"), ent.Owner, ent.Owner);
    }
}
