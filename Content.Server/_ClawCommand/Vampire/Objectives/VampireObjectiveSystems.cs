// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Objectives.Components;

namespace Content.Server._ClawCommand.Vampire.Objectives;

public sealed partial class VampireLairConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VampireLairConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, VampireLairConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = args.Mind.OwnedEntity is { } body && TryComp<VampireComponent>(body, out var vampire) && vampire.ClaimedCoffin != null
            ? 1f
            : 0f;
    }
}

public sealed partial class VampireProtegeConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VampireProtegeConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, VampireProtegeConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = args.Mind.OwnedEntity is { } body && TryComp<VampireComponent>(body, out var vampire) && vampire.Vassals.Count > 0
            ? 1f
            : 0f;
    }
}

public sealed partial class VampireBloodHarvestConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VampireBloodHarvestConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, VampireBloodHarvestConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (args.Mind.OwnedEntity is not { } body || !TryComp<VampireComponent>(body, out var vampire) || comp.Goal <= 0)
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = Math.Clamp((float) vampire.FedOn.Count / comp.Goal, 0f, 1f);
    }
}
