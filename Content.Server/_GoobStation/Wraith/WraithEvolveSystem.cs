// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: see EvolveComponent.cs - replaced Content.Shared._White.RadialSelector with the shared
// WraithRadialEntry/SimpleRadialMenu plumbing. Evolution targets (Harbinger/Plaguebringer, and ultimately
// Revenant via WraithPossessedSystem.StartPossession(makeRev: true)) are plain entity spawns, so this
// otherwise matches source's Spawn + CopyComponents + mind-transfer + delete pattern exactly.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared._GoobStation.Wraith.UI;
using Content.Server.Actions;
using Content.Server.Mind;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._GoobStation.Wraith;

/// <summary>
/// This handles evolving into a higher form with Wraith.
/// </summary>
public sealed partial class WraithEvolveSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvolveComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EvolveComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<EvolveComponent, WraithEvolveEvent>(OnWraithEvolve);
        SubscribeLocalEvent<EvolveComponent, WraithRadialSelectedMessage>(OnWraithEvolveReceived);

        SubscribeLocalEvent<AbsorbCorpseComponent, WraithEvolveAttemptEvent>(OnWraithEvolveAttempt);
    }

    private void OnMapInit(Entity<EvolveComponent> ent, ref MapInitEvent args) =>
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEnt, ent.Comp.ActionId);

    private void OnShutdown(Entity<EvolveComponent> ent, ref ComponentShutdown args) =>
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEnt);

    private void OnWraithEvolve(Entity<EvolveComponent> ent, ref WraithEvolveEvent args)
    {
        var ev = new WraithEvolveAttemptEvent(ent.Comp.CorpsesRequired);
        RaiseLocalEvent(ent.Owner, ref ev);

        if (ev.Cancelled)
            return;

        _ui.TryToggleUi(ent.Owner, WraithEvolveUiKey.Key, ent.Owner);
        _ui.SetUiState(ent.Owner, WraithEvolveUiKey.Key, new WraithRadialBoundUserInterfaceState(ent.Comp.AvailableEvolutions));

        args.Handled = true;
    }

    private void OnWraithEvolveReceived(Entity<EvolveComponent> ent, ref WraithRadialSelectedMessage args)
    {
        Evolve(ent, args.SelectedId);

        _ui.CloseUi(ent.Owner, WraithEvolveUiKey.Key);
    }

    private void Evolve(Entity<EvolveComponent> ent, string? evolve)
    {
        var uid = ent.Owner;
        if (evolve == null
            || !IsAvailableEvolution(ent.Comp.AvailableEvolutions, evolve)
            || !_proto.TryIndex<EntityPrototype>(evolve, out _)
            || !_mind.TryGetMind(uid, out var mindUid, out var mind))
            return;

        var coordinates = _transformSystem.GetMoverCoordinates(uid);
        var newForm = Spawn(evolve, coordinates);

        var meta = MetaData(uid);
        _meta.SetEntityName(newForm, meta.EntityName);

        _mind.TransferTo(mindUid, newForm, mind: mind);
        _mind.UnVisit(mindUid, mind);

        CopyComps(uid, newForm);

        _admin.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(ent.Owner)} evolved to {ToPrettyString(newForm)} as a Wraith");

        RemComp<EvolveComponent>(newForm);
        Del(uid);
    }

    private static bool IsAvailableEvolution(List<WraithRadialEntry> entries, string evolve)
    {
        foreach (var entry in entries)
        {
            if (entry.Id == evolve)
                return true;
        }

        return false;
    }

    private void OnWraithEvolveAttempt(Entity<AbsorbCorpseComponent> ent, ref WraithEvolveAttemptEvent args)
    {
        if (ent.Comp.CorpsesAbsorbed < args.CorpsesRequired)
        {
            _popups.PopupEntity(Loc.GetString("wraith-evolve-not-enough", ("corpseCount", args.CorpsesRequired)), ent.Owner, ent.Owner);
            args.Cancelled = true;
        }
    }
}
