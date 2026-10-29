// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source gibbed via Content.Shared.Body.Systems.SharedBodySystem.GibBody, which doesn't exist
// on this fork. Substituted with Content.Shared.Gibbing.GibbingSystem.Gib, this fork's actual gib entry
// point (also used e.g. by the already-ported Devil/Slasher antags).

using Content.Shared._GoobStation.Wraith.Curses;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._GoobStation.Wraith.Revenant;

/// <summary>
/// The target immediately collapses and begins to take a huge amount of brute damage over time
/// as their bones crack and their body implodes. The victim is gibbed once this damage becomes lethal,
/// but the process is interrupted if they are removed from your line of sight or you move (or are moved).
/// </summary>
public sealed partial class RevenantCrushSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stunSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantCrushComponent, RevenantCrushEvent>(OnRevenantCrush);
        SubscribeLocalEvent<RevenantCrushComponent, RevenantCrushDoAfterEvent>(OnRevenantCrushDoAfter);
    }

    private void OnRevenantCrush(Entity<RevenantCrushComponent> ent, ref RevenantCrushEvent args)
    {
        if (ent.Comp.InitialDamage == null)
            return;

        if (HasComp<CurseImmuneComponent>(args.Target))
        {
            _popup.PopupClient(Loc.GetString("revenant-crush-chaplain"), ent.Owner, ent.Owner);
            return;
        }

        var doAftersArgs = new DoAfterArgs(
            EntityManager,
            ent.Owner,
            ent.Comp.AbilityDuration,
            new RevenantCrushDoAfterEvent(),
            ent.Owner,
            args.Target)
        {
            // technically, in order for them to be removed from our line of sight, they need to move...
            BreakOnMove = true,
            MovementThreshold = 0.3f,
            DistanceThreshold = 15f,
        };

        _popup.PopupClient(Loc.GetString("revenant-crush-start"), args.Target, args.Target, PopupType.MediumCaution);
        _doAfter.TryStartDoAfter(doAftersArgs);
        _audio.PlayPredicted(ent.Comp.CrushSound, args.Target, args.Target);

        _stunSystem.TryKnockdown(args.Target, ent.Comp.KnockdownDuration, true);
        _damageableSystem.TryChangeDamage(args.Target, ent.Comp.InitialDamage, true);

        args.Handled = true;
    }

    private void OnRevenantCrushDoAfter(Entity<RevenantCrushComponent> ent, ref RevenantCrushDoAfterEvent args)
    {
        var target = args.Target;

        if (args.Cancelled || target == null)
            return;

        _popup.PopupClient(Loc.GetString("revenant-crush-you"), target.Value, target.Value);
        _admin.Add(LogType.Gib, LogImpact.High, $"{ent.Owner} gibbed {target.Value} via Crush");
        if (_netManager.IsServer)
            _gibbing.Gib(target.Value);

        args.Handled = true;
    }
}
