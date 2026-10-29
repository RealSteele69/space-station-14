// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.Actions;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Systems;

public sealed partial class SlasherSummonMacheteSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherSummonMacheteComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlasherSummonMacheteComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SlasherSummonMacheteComponent, SlasherSummonMacheteEvent>(OnSummon);
    }

    private void OnMapInit(Entity<SlasherSummonMacheteComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEnt, ent.Comp.ActionId);
    }

    private void OnShutdown(Entity<SlasherSummonMacheteComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEnt);
    }

    /// <summary>
    /// Handles summoning the Machete.
    /// </summary>
    private void OnSummon(Entity<SlasherSummonMacheteComponent> ent, ref SlasherSummonMacheteEvent args)
    {
        if (!TryComp<HandsComponent>(ent.Owner, out var hands) || hands.Hands.Count == 0)
        {
            _popup.PopupPredicted(Loc.GetString("slasher-machete-no-hands"), ent.Owner, ent.Owner);
            return;
        }

        var machete = ent.Comp.MacheteUid;

        if (machete != null && Exists(machete.Value) && _hands.IsHolding((ent.Owner, hands), machete.Value, out _))
        {
            _popup.PopupPredicted(Loc.GetString("slasher-machete-already-holding"), ent.Owner, ent.Owner);
            return;
        }

        if (!_hands.TryGetEmptyHand((ent.Owner, hands), out _))
        {
            _popup.PopupPredicted(Loc.GetString("slasher-machete-hands-full"), ent.Owner, ent.Owner);
            return;
        }

        if (_net.IsServer)
        {
            if (machete == null || Deleted(machete))
            {
                if (!_protos.TryIndex(ent.Comp.MachetePrototype, out EntityPrototype? _))
                    return;

                machete = Spawn(ent.Comp.MachetePrototype, _xform.GetMoverCoordinates(ent.Owner));
                ent.Comp.MacheteUid = machete;
                Dirty(ent);
            }

            _hands.TryPickupAnyHand(ent.Owner, machete.Value);
        }

        args.Handled = true;
    }
}
