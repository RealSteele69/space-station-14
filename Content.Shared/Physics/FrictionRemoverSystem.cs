using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Physics;

/// <summary>
/// Zeroes physics damping on bodies in open space so that objects launched into space
/// keep their momentum instead of being slowed to a halt by the physics solver.
/// Part of the Frictionless Space port (#464).
/// </summary>
/// <remarks>
/// Claw Command - this used to zero damping on every body that fell asleep, grid-bound ones included.
/// That rewrote damping on nearly everything on a station (bloating map saves with linearDamping: 0)
/// for no benefit, since tile friction is what stops things on a grid. Damping is now only removed
/// while a body is off-grid, and the prototype's value comes back when it lands on a grid again.
/// </remarks>
public sealed partial class FrictionRemoverSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<PhysicsComponent, PhysicsSleepEvent>(OnSleep);
        SubscribeLocalEvent<EntParentChangedMessage>(OnParentChanged);
    }

    private void OnSleep(EntityUid uid, PhysicsComponent component, ref PhysicsSleepEvent args)
    {
        if (IsInSpace(Transform(uid)))
            RemoveDamping(uid, component);
    }

    private void OnParentChanged(ref EntParentChangedMessage args)
    {
        if (!_physicsQuery.TryComp(args.Entity, out var physics))
            return;

        if (IsInSpace(args.Transform))
            RemoveDamping(args.Entity, physics);
        else if (args.Transform.GridUid != null)
            RestoreDamping(args.Entity, physics);
    }

    private static bool IsInSpace(TransformComponent xform)
    {
        return xform.GridUid == null && xform.MapUid != null;
    }

    private void RemoveDamping(EntityUid uid, PhysicsComponent component)
    {
        _physics.SetAngularDamping(uid, component, 0f, false);
        _physics.SetLinearDamping(uid, component, 0f);
    }

    private void RestoreDamping(EntityUid uid, PhysicsComponent component)
    {
        // Only undo what RemoveDamping did; leave anything else that set damping alone.
        if (component.LinearDamping != 0f || component.AngularDamping != 0f)
            return;

        if (MetaData(uid).EntityPrototype is not { } proto
            || !proto.TryComp<PhysicsComponent>(out var protoPhysics, EntityManager.ComponentFactory))
            return;

        _physics.SetAngularDamping(uid, component, protoPhysics.AngularDamping, false);
        _physics.SetLinearDamping(uid, component, protoPhysics.LinearDamping);
    }
}
