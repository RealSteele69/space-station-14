using Content.Shared._ClawCommand.Revenant;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._ClawCommand.Revenant;

public sealed partial class RevealRevenantOnCollideSystem : SharedRevealRevenantOnCollideSystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private CollisionWakeSystem _collisionWake = default!;

    private const string FixtureId = "revenantReveal";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevealRevenantOnCollideComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RevealRevenantOnCollideComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, RevealRevenantOnCollideComponent comp, ComponentStartup args)
    {
        var fixtures = EnsureComp<FixturesComponent>(uid);
        EnsureComp<PhysicsComponent>(uid);

        _fixtures.TryCreateFixture(uid,
            new PhysShapeCircle(0.35f),
            FixtureId,
            hard: false,
            collisionMask: (int) CollisionGroup.GhostImpassable,
            collisionLayer: (int) CollisionGroup.GhostImpassable,
            manager: fixtures
        );

        // Disable collision wake so the fixture keeps triggering collisions while held still.
        var collisionWake = EnsureComp<CollisionWakeComponent>(uid);
        _collisionWake.SetEnabled(uid, false, collisionWake);
    }

    private void OnShutdown(EntityUid uid, RevealRevenantOnCollideComponent comp, ComponentShutdown args)
    {
        _fixtures.DestroyFixture(uid, FixtureId);
    }
}
