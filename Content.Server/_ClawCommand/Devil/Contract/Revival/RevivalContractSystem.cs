// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Mind;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Devil.Condemned;
using Content.Shared._ClawCommand.Devil.UI;
using Content.Shared.Administration.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Server.Player;

namespace Content.Server._ClawCommand.Devil.Contract.Revival;

/// <summary>
/// A contract the devil uses on a corpse: the dead player's ghost is asked whether to sell their soul for a revival.
/// </summary>
[RegisterComponent]
public sealed partial class RevivalContractComponent : Component
{
    /// <summary>
    /// The entity who signed the paper, AKA, the entity who has the effects applied.
    /// </summary>
    [DataField]
    public EntityUid? Signer;

    /// <summary>
    /// The entity who created the contract, AKA, the entity who gains the soul.
    /// </summary>
    [DataField]
    public EntityUid? ContractOwner;
}

/// <summary>
/// Added to a ghost while it has a revival offer waiting for an answer.
/// </summary>
[RegisterComponent]
public sealed partial class PendingRevivalContractComponent : Component
{
    /// <summary>
    /// The entity being revived.
    /// </summary>
    [ViewVariables]
    public EntityUid? Contractee;

    /// <summary>
    /// The entity offering revival
    /// </summary>
    [ViewVariables]
    public EntityUid? Offerer;

    /// <summary>
    /// The contract attached to this player.
    /// </summary>
    [ViewVariables]
    public EntityUid? Contract;

    /// <summary>
    /// The MindId of the player.
    /// </summary>
    [ViewVariables]
    public EntityUid MindId;
}

public sealed partial class RevivalContractSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private DevilContractSystem _contract = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevivalContractComponent, AfterInteractEvent>(AfterInteract);
        SubscribeLocalEvent<PendingRevivalContractComponent, RevivalContractMessage>(OnMessage);
    }

    private void AfterInteract(Entity<RevivalContractComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target is not { } target
            || !TryComp<MobStateComponent>(target, out var mobState)
            || mobState.CurrentState != MobState.Dead)
            return;

        args.Handled = true;

        // Non-devils can't offer deals silly.
        if (!HasComp<DevilComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("devil-sign-invalid-user"), args.User, args.User, PopupType.MediumCaution);
            return;
        }

        // Make sure the mind actually exists
        if (!_mind.TryGetMind(target, out var mindId, out var mindComp) || mindComp.CurrentEntity is not { } ghost)
        {
            _popup.PopupEntity(Loc.GetString("revival-contract-no-mind"), args.User, args.User);
            return;
        }

        // You can't offer two deals at once.
        if (HasComp<PendingRevivalContractComponent>(ghost) || HasComp<CondemnedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("revival-contract-use-failed", ("target", target)), args.User, args.User);
            return;
        }

        var pending = EnsureComp<PendingRevivalContractComponent>(ghost);
        pending.Contractee = target;
        pending.Offerer = args.User;
        pending.Contract = ent;
        pending.MindId = mindId;

        _popup.PopupEntity(Loc.GetString("revival-contract-use-success", ("target", target)), args.User, args.User);

        ent.Comp.Signer = target;
        ent.Comp.ContractOwner = args.User;

        TryOpenUi(ghost, mindComp.UserId);
    }

    private void TryOpenUi(EntityUid ghost, Robust.Shared.Network.NetUserId? userId)
    {
        if (!_userInterface.HasUi(ghost, RevivalContractUiKey.Key)
            || userId is not { } id
            || !_player.TryGetSessionById(id, out var session))
            return;

        _userInterface.OpenUi(ghost, RevivalContractUiKey.Key, session);
    }

    private void OnMessage(Entity<PendingRevivalContractComponent> ent, ref RevivalContractMessage args)
    {
        if (args.Accepted && ent.Comp.Contractee is { } contractee && TryReviveAndTransferSoul(contractee, ent.Comp))
            _mind.UnVisit(ent.Comp.MindId);
        else if (!args.Accepted && ent.Comp.Offerer is { } offerer)
            _popup.PopupEntity(Loc.GetString("revival-contract-rejected"), offerer, offerer);

        RemComp<PendingRevivalContractComponent>(ent);
    }

    private bool TryReviveAndTransferSoul(EntityUid target, PendingRevivalContractComponent pending)
    {
        if (TerminatingOrDeleted(target)
            || !TryComp<RevivalContractComponent>(pending.Contract, out var contract)
            || contract is not { ContractOwner: { Valid: true } contractOwner, Signer: { } signer })
            return false;

        _rejuvenate.PerformRejuvenate(target);
        _popup.PopupEntity(Loc.GetString("revival-contract-accepted"), target, target);
        _contract.TryTransferSouls(contractOwner, signer, 1);

        // One contract, one soul.
        QueueDel(pending.Contract);
        return true;
    }
}
