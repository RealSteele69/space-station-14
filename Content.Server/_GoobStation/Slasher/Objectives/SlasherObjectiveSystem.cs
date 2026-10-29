// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Server.Objectives.Systems;
using Content.Shared._GoobStation.Slasher.Objectives;
using Content.Shared.Objectives.Components;

namespace Content.Server._GoobStation.Slasher.Objectives;

/// <summary>
/// Handles Slasher-specific objectives.
/// </summary>
public sealed partial class SlasherObjectiveSystem : EntitySystem
{
    [Dependency] private NumberObjectiveSystem _number = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherAbsorbSoulsConditionComponent, ObjectiveGetProgressEvent>(OnGetAbsorbSoulsProgress);
    }

    private void OnGetAbsorbSoulsProgress(EntityUid uid, SlasherAbsorbSoulsConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = _number.GetTarget(uid) != 0 ? MathF.Min(comp.Absorbed / (float) _number.GetTarget(uid), 1f) : 1f;
    }
}
