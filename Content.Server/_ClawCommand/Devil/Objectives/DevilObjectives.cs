// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._ClawCommand.Devil.Contract;
using Content.Server._ClawCommand.Devil.Objectives.Systems;
using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Server._ClawCommand.Devil.Objectives.Components
{
    /// <summary>
    /// Collect a number of souls.
    /// </summary>
    [RegisterComponent, Access(typeof(DevilSystem), typeof(DevilObjectiveSystem))]
    public sealed partial class SignContractConditionComponent : Component
    {
        [DataField]
        public int ContractsSigned;
    }

    /// <summary>
    /// Reach a total contract weight (how much the deals favoured the devil).
    /// </summary>
    [RegisterComponent, Access(typeof(DevilContractSystem), typeof(DevilObjectiveSystem))]
    public sealed partial class MeetContractWeightConditionComponent : Component
    {
        [DataField]
        public int ContractWeight;
    }
}

namespace Content.Server._ClawCommand.Devil.Objectives.Systems
{
    using Content.Server._ClawCommand.Devil.Objectives.Components;

    public sealed partial class DevilObjectiveSystem : EntitySystem
    {
        [Dependency] private NumberObjectiveSystem _number = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SignContractConditionComponent, ObjectiveGetProgressEvent>(OnContractGetProgress);
            SubscribeLocalEvent<MeetContractWeightConditionComponent, ObjectiveGetProgressEvent>(OnWeightGetProgress);
        }

        private void OnContractGetProgress(EntityUid uid, SignContractConditionComponent comp, ref ObjectiveGetProgressEvent args)
        {
            var target = _number.GetTarget(uid);
            args.Progress = target != 0 ? MathF.Min((float) comp.ContractsSigned / target, 1f) : 1f;
        }

        private void OnWeightGetProgress(EntityUid uid, MeetContractWeightConditionComponent comp, ref ObjectiveGetProgressEvent args)
        {
            var target = _number.GetTarget(uid);
            args.Progress = target != 0 ? MathF.Min((float) comp.ContractWeight / target, 1f) : 1f;
        }
    }
}
