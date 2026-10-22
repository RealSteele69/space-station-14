// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._ClawCommand.Devil.Contract.Revival;
using Content.Server._ClawCommand.Devil.Grip;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Devil.Actions;
using Content.Shared._ClawCommand.Devil.Condemned;
using Content.Shared._ClawCommand.Devil.Contract;
using Content.Shared.Cuffs.Components;
using Content.Shared.IdentityManagement;

namespace Content.Server._ClawCommand.Devil;

public sealed partial class DevilSystem
{
    private void SubscribeAbilities()
    {
        SubscribeLocalEvent<DevilComponent, CreateContractEvent>(OnContractCreated);
        SubscribeLocalEvent<DevilComponent, CreateRevivalContractEvent>(OnRevivalContractCreated);
        SubscribeLocalEvent<DevilComponent, ShadowJauntEvent>(OnShadowJaunt);
        SubscribeLocalEvent<DevilComponent, DevilGripEvent>(OnDevilGrip);
        SubscribeLocalEvent<DevilComponent, DevilPossessionEvent>(OnPossess);
    }

    private void OnContractCreated(Entity<DevilComponent> devil, ref CreateContractEvent args)
    {
        if (!TryUseAbility(args))
            return;

        var contract = Spawn(devil.Comp.ContractPrototype, Transform(devil).Coordinates);
        _hands.TryPickupAnyHand(devil, contract);

        if (TryComp<DevilContractComponent>(contract, out var contractComponent))
            contractComponent.ContractOwner = args.Performer;

        PlayFwooshSound(devil, devil.Comp);
        DoContractFlavor(devil, Identity.Name(devil, EntityManager));
    }

    private void OnRevivalContractCreated(Entity<DevilComponent> devil, ref CreateRevivalContractEvent args)
    {
        if (!TryUseAbility(args))
            return;

        var contract = Spawn(devil.Comp.RevivalContractPrototype, Transform(devil).Coordinates);
        _hands.TryPickupAnyHand(devil, contract);

        if (TryComp<RevivalContractComponent>(contract, out var contractComponent))
            contractComponent.ContractOwner = args.Performer;

        PlayFwooshSound(devil, devil.Comp);
        DoContractFlavor(devil, Identity.Name(devil, EntityManager));
    }

    private void OnShadowJaunt(Entity<DevilComponent> devil, ref ShadowJauntEvent args)
    {
        if (!TryUseAbility(args))
            return;

        Spawn(devil.Comp.JauntAnimationProto, Transform(devil).Coordinates);
        Spawn(devil.Comp.PentagramEffectProto, Transform(devil).Coordinates);

        // Handcuffs don't follow you into the shadows.
        if (TryComp<CuffableComponent>(devil, out var cuffable))
            _container.EmptyContainer(cuffable.Container, true);

        _poly.PolymorphEntity(devil, devil.Comp.JauntEntityProto);
    }

    private void OnDevilGrip(Entity<DevilComponent> devil, ref DevilGripEvent args)
    {
        if (!TryUseAbility(args))
            return;

        // Using the action again while holding the grip puts it away.
        foreach (var item in _hands.EnumerateHeld(devil.Owner))
        {
            if (!HasComp<DevilGripComponent>(item))
                continue;

            QueueDel(item);
            return;
        }

        var grip = Spawn(devil.Comp.GripPrototype, Transform(devil).Coordinates);
        if (!_hands.TryPickupAnyHand(devil, grip))
        {
            QueueDel(grip);
            return;
        }

        devil.Comp.DevilGrip = args.Action.Owner;
    }

    private void OnPossess(Entity<DevilComponent> devil, ref DevilPossessionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CondemnedComponent>(args.Target, out var condemned) || condemned.SoulOwnedNotDevil)
        {
            _popup.PopupEntity(Loc.GetString("invalid-possession-target"), devil, devil);
            return;
        }

        // Stronger devils possess for longer.
        var duration = devil.Comp.PossessionDuration;
        if (devil.Comp.PowerLevel != DevilPowerLevel.None)
            duration *= (int) devil.Comp.PowerLevel;

        // Only mark as handled if possession succeeds to avoid wasting the cooldown.
        if (!_possession.TryPossessTarget(args.Target, args.Performer, duration, true, polymorphPossessor: true))
            return;

        Spawn(devil.Comp.JauntAnimationProto, Transform(args.Target).Coordinates);
        Spawn(devil.Comp.PentagramEffectProto, Transform(args.Target).Coordinates);

        args.Handled = true;
    }
}
