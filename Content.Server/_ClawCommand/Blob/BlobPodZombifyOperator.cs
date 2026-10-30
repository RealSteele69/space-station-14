using Content.Server._ClawCommand.Blob.NPC.BlobPod;
using Content.Shared._ClawCommand.Blob.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._ClawCommand.Blob;

public sealed partial class BlobPodZombifyOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private BlobPodSystem _blobPodSystem = default!;

    [DataField("zombifyKey")]
    public string ZombifyKey = string.Empty;

    public override void Initialize(IDependencyCollection deps)
    {
        var sysManager = deps.Resolve<IEntitySystemManager>();
        base.Initialize(deps);
        _blobPodSystem = sysManager.GetEntitySystem<BlobPodSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var target = blackboard.GetValue<EntityUid>(ZombifyKey);

        if (!target.IsValid() || _entManager.Deleted(target))
            return HTNOperatorStatus.Failed;

        if (!_entManager.TryGetComponent<BlobPodComponent>(owner, out var pod))
            return HTNOperatorStatus.Failed;

        if (pod.ZombifiedEntityUid != null)
            return HTNOperatorStatus.Continuing;

        if (pod.IsZombifying)
            return HTNOperatorStatus.Continuing;

        if (pod.ZombifyTarget == null)
        {
            if (_blobPodSystem.NpcStartZombify(owner, target, pod))
                return HTNOperatorStatus.Continuing;
            else
                return HTNOperatorStatus.Failed;
        }

        pod.ZombifyTarget = null;
        return HTNOperatorStatus.Finished;
    }
}