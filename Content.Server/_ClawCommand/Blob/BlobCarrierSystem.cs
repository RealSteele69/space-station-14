using Content.Shared._ClawCommand.Blob;
using Content.Shared._ClawCommand.Blob.Components;
using Content.Shared._ClawCommand.Blob.Events;
using Content.Server.Actions;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._ClawCommand.Blob.Roles;
using Content.Shared.Gibbing;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Blob;

public sealed partial class BlobCarrierSystem : SharedBlobCarrierSystem
{
    [Dependency] private BlobCoreSystem _blobCoreSystem = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private GhostRoleSystem _ghost = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private ActionsSystem _action = default!;
    [Dependency] private RoleSystem _role = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobCarrierComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BlobCarrierComponent, TransformToBlobActionEvent>(OnTransformToBlobChanged);

        SubscribeLocalEvent<BlobCarrierComponent, MapInitEvent>(OnStartup);

        SubscribeLocalEvent<BlobCarrierComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<BlobCarrierComponent, MindRemovedMessage>(OnMindRemove);
    }

    private static readonly EntProtoId ActionTransformToBlob = "ActionTransformToBlob";
    private static readonly EntProtoId MindRoleBlob = "MindRoleBlob";

    /// <summary>
    /// Gives the carrier's mind the blob role (once), so it shows as an antagonist however it became a carrier:
    /// roundstart selection, the midround mouse ghost role or the admin verb.
    /// </summary>
    private void EnsureBlobRole(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _) || _role.MindHasRole<BlobRoleComponent>(mindId))
            return;

        _role.MindAddRole(mindId, MindRoleBlob, silent: true);
    }

    private void OnMindAdded(EntityUid uid, BlobCarrierComponent component, MindAddedMessage args)
    {
        component.HasMind = true;
        EnsureBlobRole(uid);
    }

    private void OnMindRemove(EntityUid uid, BlobCarrierComponent component, MindRemovedMessage args) => component.HasMind = false;

    private void OnTransformToBlobChanged(Entity<BlobCarrierComponent> uid, ref TransformToBlobActionEvent args) => TransformToBlob(uid);

    private void OnStartup(EntityUid uid, BlobCarrierComponent component, MapInitEvent args)
    {
        _action.AddAction(uid, ref component.TransformToBlob, ActionTransformToBlob);

        if (HasComp<ActorComponent>(uid))
        {
            EnsureBlobRole(uid);
            return;
        }

        var ghostRole = EnsureComp<GhostRoleComponent>(uid);
        EnsureComp<GhostTakeoverAvailableComponent>(uid);
        ghostRole.RoleName = Loc.GetString("blob-carrier-role-name");
        ghostRole.RoleDescription = Loc.GetString("blob-carrier-role-desc");
        ghostRole.RoleRules = Loc.GetString("blob-carrier-role-rules");
    }

    private void OnMobStateChanged(Entity<BlobCarrierComponent> uid, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            TransformToBlob(uid);
        }
    }

    protected override void TransformToBlob(Entity<BlobCarrierComponent> ent)
    {
        var xform = Transform(ent);
        if (!HasComp<MapGridComponent>(xform.GridUid))
            return;

        if (_mind.TryGetMind(ent, out _, out var mind) && mind.UserId != null)
        {
            var core = Spawn(ent.Comp.CoreBlobPrototype, xform.Coordinates);
            var ghostRoleComp = EnsureComp<GhostRoleComponent>(core);

            // Unfortunately we have to manually turn this off so we don't need to make more prototypes.
            _ghost.UnregisterGhostRole((core, ghostRoleComp));

            if (!TryComp<BlobCoreComponent>(core, out var blobCoreComponent))
                return;

            _blobCoreSystem.CreateBlobObserver(core, mind.UserId.Value, blobCoreComponent);
        }
        else
        {
            Spawn(ent.Comp.CoreBlobPrototype, xform.Coordinates);
        }

        // Drop the carrier first so the death from gibbing can't spawn a second core.
        RemComp<BlobCarrierComponent>(ent);
        _gibbing.Gib(ent);
    }
}
