// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Religion;
using Content.Server.Polymorph.Systems;
using Content.Shared._ClawCommand.CheatDeath;
using Content.Shared._ClawCommand.Devil.Condemned;
using Content.Shared.ActionBlocker;
using Content.Shared.Body;
using Content.Shared.DetailExaminable;
using Content.Shared.Examine;
using Content.Shared.Forensics.Systems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Spawners;

namespace Content.Server._ClawCommand.Devil.Condemned;

public sealed partial class CondemnedSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PolymorphSystem _poly = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DevilSystem _devil = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private ForensicsSystem _forensics = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CondemnedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CondemnedComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<CondemnedComponent, ComponentRemove>(OnRemoved);
        SubscribeLocalEvent<CondemnedComponent, UpdateCanMoveEvent>(OnMoveAttempt);
        SubscribeLocalEvent<CondemnedComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CondemnedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            switch (comp.CurrentPhase)
            {
                case CondemnedPhase.PentagramActive:
                    UpdatePentagramPhase(uid, frameTime, comp);
                    break;
                case CondemnedPhase.HandActive:
                    UpdateHandPhase(uid, frameTime, comp);
                    break;
            }
        }
    }

    private void OnStartup(Entity<CondemnedComponent> condemned, ref MapInitEvent args)
    {
        if (condemned.Comp.SoulOwnedNotDevil)
            return;

        if (HasComp<WeakToHolyComponent>(condemned))
            condemned.Comp.WasWeakToHoly = true;
        else
            EnsureComp<WeakToHolyComponent>(condemned).AlwaysTakeHoly = true;
    }

    private void OnRemoved(Entity<CondemnedComponent> condemned, ref ComponentRemove args)
    {
        if (condemned.Comp.SoulOwnedNotDevil || TerminatingOrDeleted(condemned))
            return;

        if (!condemned.Comp.WasWeakToHoly)
            RemComp<WeakToHolyComponent>(condemned);
    }

    private void OnMoveAttempt(Entity<CondemnedComponent> condemned, ref UpdateCanMoveEvent args)
    {
        if (!condemned.Comp.FreezeDuringCondemnation
            || condemned.Comp.CurrentPhase is CondemnedPhase.Waiting or CondemnedPhase.Complete)
            return;

        args.Cancel();
    }

    private void OnMobStateChanged(EntityUid uid, CondemnedComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead
            || comp.SoulOwnedNotDevil
            || !comp.CondemnOnDeath)
            return;

        // Still got a way out.
        if (TryComp<CheatDeathComponent>(uid, out var cheatDeath)
            && (cheatDeath.ReviveAmount > 0 || cheatDeath.InfiniteRevives))
            return;

        StartCondemnation(uid, behavior: CondemnedBehavior.Delete);
    }

    public void StartCondemnation(
        EntityUid uid,
        bool freezeEntity = true,
        bool doFlavor = true,
        CondemnedBehavior behavior = CondemnedBehavior.Delete)
    {
        var comp = EnsureComp<CondemnedComponent>(uid);
        comp.CondemnOnDeath = false;
        comp.CondemnedBehavior = behavior;

        if (freezeEntity)
            comp.FreezeDuringCondemnation = true;

        var coords = Transform(uid).Coordinates;
        Spawn(comp.PentagramProto, coords);
        _audio.PlayPvs(comp.SoundEffect, coords);

        if (behavior == CondemnedBehavior.Delete && doFlavor)
            _popup.PopupCoordinates(Loc.GetString("condemned-start", ("target", uid)), coords, PopupType.LargeCaution);

        comp.CurrentPhase = CondemnedPhase.PentagramActive;
        comp.PhaseTimer = 0f;
        _actionBlocker.UpdateCanMove(uid);
    }

    private void UpdatePentagramPhase(EntityUid uid, float frameTime, CondemnedComponent comp)
    {
        comp.PhaseTimer += frameTime;

        if (comp.PhaseTimer < 3f)
            return;

        var handEntity = Spawn(comp.HandProto, Transform(uid).Coordinates);

        comp.HandDuration = TryComp<TimedDespawnComponent>(handEntity, out var timedDespawn)
            ? timedDespawn.Lifetime
            : 1f;

        comp.CurrentPhase = CondemnedPhase.HandActive;
        comp.PhaseTimer = 0f;
    }

    private void UpdateHandPhase(EntityUid uid, float frameTime, CondemnedComponent comp)
    {
        comp.PhaseTimer += frameTime;

        if (comp.PhaseTimer < comp.HandDuration)
            return;

        comp.CurrentPhase = CondemnedPhase.Complete;
        _actionBlocker.UpdateCanMove(uid);
        DoCondemnedBehavior(uid, comp);
    }

    private void DoCondemnedBehavior(EntityUid uid, CondemnedComponent comp)
    {
        switch (comp.CondemnedBehavior)
        {
            case CondemnedBehavior.Delete:
                QueueDel(uid);
                return;
            case CondemnedBehavior.Banish:
                if (comp.ScrambleAfterBanish)
                    ScrambleDna(uid);
                RemCompDeferred(uid, comp);
                _poly.PolymorphEntity(uid, comp.BanishProto);
                break;
        }
    }

    /// <summary>
    /// The same scramble a DNA scrambler implant does: new random appearance, name, DNA and fingerprints.
    /// </summary>
    private void ScrambleDna(EntityUid target)
    {
        if (!TryComp<HumanoidProfileComponent>(target, out var humanoid))
            return;

        var newProfile = HumanoidCharacterProfile.RandomWithSpecies(humanoid.Species);
        _visualBody.ApplyProfileTo(target, newProfile);
        _humanoidProfile.ApplyProfileTo(target, newProfile);
        _metaData.SetEntityName(target, newProfile.Name, raiseEvents: false);

        _forensics.RandomizeDNA(target);
        _forensics.RandomizeFingerprint(target);

        RemComp<DetailExaminableComponent>(target);
        _identity.QueueIdentityUpdate(target);
    }

    private void OnExamined(Entity<CondemnedComponent> condemned, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange
            || condemned.Comp.SoulOwnedNotDevil
            || !condemned.Comp.ShowExamineMessage
            || _devil.IsEyesCovered(condemned))
            return;

        args.PushMarkup(Loc.GetString("condemned-component-examined", ("target", Identity.Entity(condemned, EntityManager))));
    }
}
