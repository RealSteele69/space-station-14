// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note (significant rewrite): source built this on top of Content.Goobstation.Shared.PhaseShift's
// generic PhaseShiftedComponent plus a long list of Goobstation-only immunity components
// (FacehuggerImmuneComponent - no facehugger/xenomorph system on this fork at all;
// MaterialReclaimerImmuneComponent - doesn't exist here), a cooldown-freeze mechanic, a
// surveillance-camera corporealize check, and a BodyPartComponent-based damage-cancel workaround
// (this fork's body system has no detachable parts to route damage through in the first place).
// Rebuilt around the pattern this fork already uses for its other "walk through walls, unseen"
// mechanic - Content.Shared._ClawCommand.BloodCult.Systems.SharedBloodCultPhaseShiftSystem (ported
// for the Blood Cult's wraith form) - using direct collision mask/layer swap + StealthComponent
// instead of a shared PhaseShift component, and canceling damage directly via
// BeforeDamageChangedEvent on the slasher itself (this fork's flat damage model doesn't need
// BodyPartComponent lookups the way Goobstation's does). Dropped the cooldown freeze and camera
// check as presentation-only polish, not core to "become unseen, walk through walls."

using Content.Goobstation.Common.Atmos;
using Content.Goobstation.Common.Temperature.Components;
using Content.Shared._ClawCommand.Supermatter.Components;
using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Flash;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._GoobStation.Slasher.Systems;

public sealed partial class SlasherIncorporealSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SlasherFearSystem _fear = default!;

    private const string FootstepSoundTag = "FootstepSound";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherIncorporealComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlasherIncorporealComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SlasherIncorporealComponent, SlasherIncorporealizeEvent>(OnIncorporealize);
        SubscribeLocalEvent<SlasherIncorporealComponent, SlasherCorporealizeEvent>(OnCorporealize);
        SubscribeLocalEvent<SlasherIncorporealComponent, SlasherIncorporealizeDoAfterEvent>(OnIncorporealizeDoAfter);

        SubscribeLocalEvent<SlasherIncorporealComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SlasherIncorporealComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<SlasherIncorporealComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<SlasherIncorporealComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<SlasherIncorporealComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<SlasherIncorporealComponent, InteractionAttemptEvent>(OnAttemptInteract);
        SubscribeLocalEvent<SlasherIncorporealComponent, PullStoppedMessage>(OnPullStopped);
        SubscribeLocalEvent<SlasherIncorporealComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<SlasherIncorporealComponent, DownAttemptEvent>(OnDownAttempt);
        SubscribeLocalEvent<SlasherIncorporealComponent, KnockDownAttemptEvent>(OnKnockDownAttempt);
        SubscribeLocalEvent<SlasherIncorporealComponent, FlashAttemptEvent>(OnFlashAttempt);
    }

    private void OnMapInit(Entity<SlasherIncorporealComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsServer)
            return;

        _actions.AddAction(ent.Owner, ref ent.Comp.IncorporealizeActionEnt, ent.Comp.IncorporealizeActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.CorporealizeActionEnt, ent.Comp.CorporealizeActionId);
        _actions.SetEnabled(ent.Comp.CorporealizeActionEnt, false);
    }

    private void OnShutdown(Entity<SlasherIncorporealComponent> ent, ref ComponentShutdown args)
    {
        if (!_net.IsServer)
            return;

        _actions.RemoveAction(ent.Owner, ent.Comp.IncorporealizeActionEnt);
        _actions.RemoveAction(ent.Owner, ent.Comp.CorporealizeActionEnt);
    }

    private void OnIncorporealize(Entity<SlasherIncorporealComponent> ent, ref SlasherIncorporealizeEvent args)
    {
        if (args.Handled || ent.Comp.IsIncorporeal)
            return;

        if (_fear.IsObservedByPlayers(ent.Owner, ent.Comp.ObserverCheckRange))
        {
            _popup.PopupPredicted(Loc.GetString("slasher-incorporealize-fail-seen"), ent.Owner, ent.Owner);
            args.Handled = true;
            return;
        }

        var doArgs = new DoAfterArgs(EntityManager,
            ent.Owner,
            ent.Comp.IncorporealizeDelay,
            new SlasherIncorporealizeDoAfterEvent(),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = false,
            BlockDuplicate = true,
            Hidden = false,
            DistanceThreshold = 1f,
        };
        _doAfter.TryStartDoAfter(doArgs);

        args.Handled = true;
    }

    private void OnCorporealize(Entity<SlasherIncorporealComponent> ent, ref SlasherCorporealizeEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.IsIncorporeal)
        {
            args.Handled = true;
            return;
        }

        if (_net.IsServer && _fear.IsObservedByPlayers(ent.Owner, ent.Comp.ObserverCheckRange))
        {
            _popup.PopupEntity(Loc.GetString("slasher-corporealize-fail-nearby"), ent.Owner, ent.Owner);
            args.Handled = true;
            return;
        }

        ExitIncorporeal(ent.Owner, ent);
        args.Handled = true;
    }

    private void OnIncorporealizeDoAfter(Entity<SlasherIncorporealComponent> ent, ref SlasherIncorporealizeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (_fear.IsObservedByPlayers(ent.Owner, ent.Comp.ObserverCheckRange))
        {
            _popup.PopupPredicted(Loc.GetString("slasher-incorporealize-fail-seen"), ent.Owner, ent.Owner);
            return;
        }

        EnterIncorporeal(ent.Owner, ent);
        args.Handled = true;
    }

    private void OnRefreshSpeed(Entity<SlasherIncorporealComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.IsIncorporeal)
            return;

        args.ModifySpeed(1.5f, 1.5f);
    }

    public void EnterIncorporeal(EntityUid uid, Entity<SlasherIncorporealComponent> ent)
    {
        ent.Comp.IsIncorporeal = true;
        Dirty(ent);

        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                ent.Comp.StoredMask = fixture.CollisionMask;
                ent.Comp.StoredLayer = fixture.CollisionLayer;
                _physics.SetCollisionMask(uid, id, fixture, ent.Comp.CollisionMask, fixtures);
                _physics.SetCollisionLayer(uid, id, fixture, ent.Comp.CollisionLayer, fixtures);
                break;
            }
        }

        ent.Comp.AddedIncorporealComponents.Clear();

        var stealth = EnsureTrackedComp<StealthComponent>(uid, ent);
        _stealth.SetVisibility(uid, stealth.MinVisibility, stealth);

        // Space/atmos immunity, reusing this fork's existing immunity marker components.
        EnsureTrackedComp<MovementIgnoreGravityComponent>(uid, ent);
        EnsureTrackedComp<SpecialPressureImmunityComponent>(uid, ent);
        EnsureTrackedComp<SpecialHighTempImmunityComponent>(uid, ent);
        EnsureTrackedComp<SpecialLowTempImmunityComponent>(uid, ent);
        EnsureTrackedComp<SupermatterImmuneComponent>(uid, ent);

        _movement.RefreshMovementSpeedModifiers(uid);

        _actions.SetEnabled(ent.Comp.IncorporealizeActionEnt, false);
        _actions.SetEnabled(ent.Comp.CorporealizeActionEnt, true);

        if (_tags.HasTag(uid, SharedDoorSystem.DoorBumpTag))
            _tags.RemoveTag(uid, SharedDoorSystem.DoorBumpTag);

        if (_tags.HasTag(uid, FootstepSoundTag))
            _tags.RemoveTag(uid, FootstepSoundTag);

        _eye.SetDrawFov(uid, false);

        if (_net.IsServer)
            _audio.PlayPvs(ent.Comp.JauntDisappear, uid);

        var enteredEv = new SlasherIncorporealEnteredEvent();
        RaiseLocalEvent(uid, ref enteredEv);
    }

    public void ExitIncorporeal(EntityUid uid, Entity<SlasherIncorporealComponent> ent)
    {
        ent.Comp.IsIncorporeal = false;
        Dirty(ent);

        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                _physics.SetCollisionMask(uid, id, fixture, ent.Comp.StoredMask, fixtures);
                _physics.SetCollisionLayer(uid, id, fixture, ent.Comp.StoredLayer, fixtures);
                break;
            }
        }

        RemoveTrackedCompsDeferred(uid, ent);

        _movement.RefreshMovementSpeedModifiers(uid);

        _actions.SetEnabled(ent.Comp.IncorporealizeActionEnt, true);
        _actions.SetEnabled(ent.Comp.CorporealizeActionEnt, false);

        _tags.AddTag(uid, SharedDoorSystem.DoorBumpTag);
        _tags.AddTag(uid, FootstepSoundTag);

        _eye.SetDrawFov(uid, true);

        if (_net.IsServer)
            _audio.PlayPvs(ent.Comp.JauntAppear, uid);
    }

    /// <summary>
    /// EnsureComp but it checks if they already had the Component.
    /// If they didn't it adds it to a list then removes it when they corporealize.
    /// </summary>
    private T EnsureTrackedComp<T>(EntityUid uid, Entity<SlasherIncorporealComponent> ent) where T : Component, new()
    {
        if (TryComp<T>(uid, out var existing))
            return existing;

        var componentName = typeof(T).FullName;
        if (componentName != null)
            ent.Comp.AddedIncorporealComponents.Add(componentName);

        return EnsureComp<T>(uid);
    }

    private void RemoveTrackedCompsDeferred(EntityUid uid, Entity<SlasherIncorporealComponent> ent)
    {
        if (ent.Comp.AddedIncorporealComponents.Count == 0)
            return;

        foreach (var component in AllComps(uid))
        {
            var componentName = component.GetType().FullName;
            if (componentName != null && ent.Comp.AddedIncorporealComponents.Contains(componentName))
                RemCompDeferred(uid, component.GetType());
        }

        ent.Comp.AddedIncorporealComponents.Clear();
    }

    // Do note that you can grab someone that's in crit or dead and then use incorporealize to drag
    // their body around while invisible but this is hilarious so I'm keeping it - Goobstation
    private void OnPullAttempt(Entity<SlasherIncorporealComponent> ent, ref PullAttemptEvent args)
    {
        if (ent.Comp.IsIncorporeal && args.PullerUid == ent.Owner)
            args.Cancelled = true;
    }

    private void OnBeforeDamage(Entity<SlasherIncorporealComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.IsIncorporeal)
            args.Cancelled = true;
    }

    private void OnDropAttempt(Entity<SlasherIncorporealComponent> ent, ref DropAttemptEvent args)
    {
        if (!ent.Comp.IsIncorporeal || HasComp<VirtualItemComponent>(args.Uid))
            return;

        args.Cancel();
    }

    private void OnAttemptInteract(Entity<SlasherIncorporealComponent> ent, ref InteractionAttemptEvent args)
    {
        if (!ent.Comp.IsIncorporeal || args.Target == null || args.Target == ent.Owner)
            return;

        if (TryComp<PullerComponent>(ent.Owner, out var puller) && puller.Pulling == args.Target
            || HasComp<VirtualItemComponent>(args.Target.Value))
            return;

        args.Cancelled = true;
    }

    private void OnPullStopped(Entity<SlasherIncorporealComponent> ent, ref PullStoppedMessage args)
    {
        if (!ent.Comp.IsIncorporeal || !TryComp<HandsComponent>(ent.Owner, out var handsComp))
            return;

        foreach (var hand in handsComp.Hands.Keys)
        {
            if (!_hands.TryGetHeldItem((ent.Owner, handsComp), hand, out var held))
                continue;

            if (!TryComp<VirtualItemComponent>(held, out var virtItem) || virtItem.BlockingEntity != args.PulledUid)
                continue;

            _virtualItem.DeleteVirtualItem((held.Value, virtItem), ent.Owner);
        }
    }

    private void OnDownAttempt(Entity<SlasherIncorporealComponent> ent, ref DownAttemptEvent args)
    {
        if (ent.Comp.IsIncorporeal)
            args.Cancel();
    }

    private void OnKnockDownAttempt(Entity<SlasherIncorporealComponent> ent, ref KnockDownAttemptEvent args)
    {
        if (ent.Comp.IsIncorporeal)
            args.Cancelled = true;
    }

    private void OnBeforeThrow(Entity<SlasherIncorporealComponent> ent, ref BeforeThrowEvent args)
    {
        if (ent.Comp.IsIncorporeal && ent.Owner == args.PlayerUid)
            args.Cancelled = true;
    }

    private void OnAttackAttempt(Entity<SlasherIncorporealComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.IsIncorporeal)
            args.Cancel();
    }

    private void OnFlashAttempt(Entity<SlasherIncorporealComponent> ent, ref FlashAttemptEvent args)
    {
        if (ent.Comp.IsIncorporeal)
            args.Cancelled = true;
    }
}
