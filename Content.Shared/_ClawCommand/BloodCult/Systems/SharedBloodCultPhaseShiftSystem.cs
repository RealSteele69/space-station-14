using System.Linq;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.BloodCult.Systems;

/// <summary>
/// Wraith phase shift: walk through walls, unseen, until it times out or the wraith strikes.
/// </summary>
public abstract partial class SharedBloodCultPhaseShiftSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultPhaseShiftedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BloodCultPhaseShiftedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BloodCultPhaseShiftedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<BloodCultPhaseShiftedComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<BloodCultPhaseShiftedComponent, ThrowAttemptEvent>(OnThrowAttempt);
    }

    /// <summary>
    /// Shifts the entity into the veil, or extends an existing shift.
    /// </summary>
    public void PhaseShift(EntityUid uid, TimeSpan duration)
    {
        var shifted = EnsureComp<BloodCultPhaseShiftedComponent>(uid);
        shifted.EndTime = Timing.CurTime + duration;
        Dirty(uid, shifted);
    }

    protected virtual void OnStartup(Entity<BloodCultPhaseShiftedComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsServer)
        {
            Spawn(ent.Comp.PhaseInEffect, _transform.GetMapCoordinates(ent));
            _audio.PlayPvs(ent.Comp.PhaseInSound, ent);
        }

        if (TryComp<FixturesComponent>(ent, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var (id, fixture) = fixtures.Fixtures.First();
            ent.Comp.StoredMask = fixture.CollisionMask;
            ent.Comp.StoredLayer = fixture.CollisionLayer;
            _physics.SetCollisionMask(ent, id, fixture, ent.Comp.CollisionMask, fixtures);
            _physics.SetCollisionLayer(ent, id, fixture, ent.Comp.CollisionLayer, fixtures);
        }

        var stealth = EnsureComp<StealthComponent>(ent);
        _stealth.SetVisibility(ent, -1f, stealth);

        if (TryComp<PullableComponent>(ent, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(ent, pullable);

        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    protected virtual void OnShutdown(Entity<BloodCultPhaseShiftedComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (_net.IsServer)
        {
            Spawn(ent.Comp.PhaseOutEffect, _transform.GetMapCoordinates(ent));
            _audio.PlayPvs(ent.Comp.PhaseOutSound, ent);
        }

        if (TryComp<FixturesComponent>(ent, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var (id, fixture) = fixtures.Fixtures.First();
            _physics.SetCollisionMask(ent, id, fixture, ent.Comp.StoredMask, fixtures);
            _physics.SetCollisionLayer(ent, id, fixture, ent.Comp.StoredLayer, fixtures);
        }

        RemComp<StealthComponent>(ent);

        ent.Comp.MovementSpeedBuff = 1f;
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<BloodCultPhaseShiftedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.MovementSpeedBuff, ent.Comp.MovementSpeedBuff);
    }

    private void OnAttackAttempt(Entity<BloodCultPhaseShiftedComponent> ent, ref AttackAttemptEvent args)
    {
        EndShift(ent);
    }

    private void OnThrowAttempt(Entity<BloodCultPhaseShiftedComponent> ent, ref ThrowAttemptEvent args)
    {
        EndShift(ent);
    }

    private void EndShift(Entity<BloodCultPhaseShiftedComponent> ent)
    {
        ent.Comp.EndTime = Timing.CurTime;
        Dirty(ent);
    }
}
