using Content.Server._ClawCommand.BloodCult.Rule;
using Content.Shared.Roles.Jobs;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Server._ClawCommand.BloodCult.Objectives;

/// <summary>
/// Sacrifice the cult's chosen target. Every cultist shares the same target.
/// </summary>
[RegisterComponent, Access(typeof(BloodCultObjectivesSystem))]
public sealed partial class KillTargetCultComponent : Component
{
    [DataField(required: true)]
    public LocId Title;

    [ViewVariables]
    public EntityUid? Target;
}

/// <summary>
/// Summon Nar'Sie. Complete once the Dimensional Rending ritual has finished.
/// </summary>
[RegisterComponent, Access(typeof(BloodCultObjectivesSystem))]
public sealed partial class SummonNarsieConditionComponent : Component
{
    [ViewVariables]
    public bool Summoned;
}

public sealed partial class BloodCultObjectivesSystem : EntitySystem
{
    [Dependency] private BloodCultRuleSystem _cultRule = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KillTargetCultComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<KillTargetCultComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<KillTargetCultComponent, ObjectiveGetProgressEvent>(OnGetProgress);

        SubscribeLocalEvent<SummonNarsieConditionComponent, ObjectiveGetProgressEvent>(OnSummonProgress);
        SubscribeLocalEvent<BloodCultNarsieSummonedEvent>(OnNarsieSummoned);
    }

    private void OnAssigned(Entity<KillTargetCultComponent> ent, ref ObjectiveAssignedEvent args)
    {
        ent.Comp.Target = _cultRule.EnsureOfferingTarget();
    }

    private void OnAfterAssign(Entity<KillTargetCultComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (ent.Comp.Target is not { } target)
            return;

        var jobName = _job.MindTryGetJobName(_mind.TryGetMind(target, out var mindId, out _) ? mindId : null);
        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("targetName", Name(target)), ("job", jobName)), args.Meta);
    }

    private void OnGetProgress(Entity<KillTargetCultComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.Target is not { } target || TerminatingOrDeleted(target))
        {
            args.Progress = 1f;
            return;
        }

        args.Progress = !HasComp<MobStateComponent>(target) || _mobState.IsDead(target) ? 1f : 0f;
    }

    private void OnSummonProgress(Entity<SummonNarsieConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Summoned ? 1f : 0f;
    }

    private void OnNarsieSummoned(BloodCultNarsieSummonedEvent ev)
    {
        var query = EntityQueryEnumerator<SummonNarsieConditionComponent>();
        while (query.MoveNext(out _, out var condition))
        {
            condition.Summoned = true;
        }
    }
}
