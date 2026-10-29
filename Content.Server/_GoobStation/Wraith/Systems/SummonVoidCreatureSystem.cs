// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: see ChooseVoidCreatureComponent.cs - replaced Content.Shared._White.RadialSelector with the
// shared WraithRadialEntry/SimpleRadialMenu plumbing.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Components.Mobs;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared._GoobStation.Wraith.UI;
using Content.Server.Actions;
using Content.Server.Mind;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._GoobStation.Wraith.Systems;

public sealed partial class SummonVoidCreatureSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SummonVoidCreatureComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SummonVoidCreatureComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SummonVoidCreatureComponent, SummonVoidCreatureEvent>(OnSummonVoidCreature);

        SubscribeLocalEvent<ChooseVoidCreatureComponent, ChooseVoidCreatureEvent>(OnChooseVoidCreature);
        SubscribeLocalEvent<ChooseVoidCreatureComponent, WraithRadialSelectedMessage>(OnSummonVoidCreatureSelected);
    }

    private void OnMapInit(Entity<SummonVoidCreatureComponent> ent, ref MapInitEvent args) =>
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEnt, ent.Comp.ActionId);

    private void OnShutdown(Entity<SummonVoidCreatureComponent> ent, ref ComponentShutdown args) =>
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEnt);

    private void OnSummonVoidCreature(Entity<SummonVoidCreatureComponent> ent, ref SummonVoidCreatureEvent args)
    {
        SpawnAtPosition(ent.Comp.SummonId, Transform(ent.Owner).Coordinates);

        args.Handled = true;
    }

    private void OnChooseVoidCreature(Entity<ChooseVoidCreatureComponent> ent, ref ChooseVoidCreatureEvent args)
    {
        _ui.TryToggleUi(ent.Owner, ChooseVoidCreatureUiKey.Key, ent.Owner);
        _ui.SetUiState(ent.Owner, ChooseVoidCreatureUiKey.Key, new WraithRadialBoundUserInterfaceState(ent.Comp.AvailableSummons));
    }

    private void OnSummonVoidCreatureSelected(Entity<ChooseVoidCreatureComponent> ent, ref WraithRadialSelectedMessage args)
    {
        var proto = args.SelectedId;
        if (!IsAvailableSummon(ent.Comp.AvailableSummons, proto)
            || !_proto.TryIndex<EntityPrototype>(proto, out _)
            || !_mind.TryGetMind(ent.Owner, out var mindUid, out var mind))
            return;

        var coordinates = _transform.GetMoverCoordinates(ent.Owner);
        var newForm = Spawn(proto, coordinates);

        _mind.TransferTo(mindUid, newForm, mind: mind);
        _mind.UnVisit(mindUid, mind);

        CopyComps(ent.Owner, newForm);
        RemComp<ChooseVoidCreatureComponent>(newForm);

        _ui.CloseUi(ent.Owner, ChooseVoidCreatureUiKey.Key);
        Del(ent.Owner);
    }

    private static bool IsAvailableSummon(List<WraithRadialEntry> entries, string proto)
    {
        foreach (var entry in entries)
        {
            if (entry.Id == proto)
                return true;
        }

        return false;
    }
}
