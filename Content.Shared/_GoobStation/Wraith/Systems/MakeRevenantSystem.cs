// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._GoobStation.Wraith.Systems;

/// <summary>
/// Handles the Harbinger "Make Revenant" ability - possessing a fresh corpse and turning it (and its
/// possessor) into a Wraith-flavored Revenant.
/// </summary>
public sealed partial class MakeRevenantSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private WraithPossessedSystem _wraithPossessed = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MakeRevenantComponent, MakeRevenantEvent>(OnMakeRevenant);
    }

    private void OnMakeRevenant(Entity<MakeRevenantComponent> ent, ref MakeRevenantEvent args)
    {
        if (!_mind.TryGetMind(ent.Owner, out var mindId, out _))
            return;

        if (!HasComp<WraithAbsorbableComponent>(args.Target)
            || !TryComp<PerishableComponent>(args.Target, out var perishComp)
            || perishComp.Stage != 1) // 1 means its a fresh corpse
        {
            _popup.PopupClient(Loc.GetString("wraith-absorb-too-decomposed"), ent.Owner, ent.Owner);
            return;
        }

        if (_netManager.IsClient)
            return;

        var rej = new RejuvenateEvent();
        RaiseLocalEvent(args.Target, rej);

        var possessed = EnsureComp<WraithPossessedComponent>(args.Target);
        possessed.RevenantDamageOvertime = ent.Comp.PassiveRevenantDamage;
        Dirty(args.Target, possessed);

        _audio.PlayPredicted(ent.Comp.PossessSound, args.Target, args.Target);
        _wraithPossessed.StartPossession((args.Target, possessed), ent.Owner, mindId, true);

        args.Handled = true;
    }
}
