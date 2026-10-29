// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.SlaughterDemon;
using Content.Shared._GoobStation.SlaughterDemon.Other;
using Content.Shared._GoobStation.SlaughterDemon.Systems;
// Fork note: both RejuvenateSystem and BloodstreamSystem live under Content.Shared here (not
// Content.Server like source), and there is no Content.Server.Body.Components.BloodstreamComponent either.
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Gibbing;
using Robust.Shared.Containers;

namespace Content.Server._GoobStation.SlaughterDemon;

public sealed partial class SlaughterDemonSystem : SharedSlaughterDemonSystem
{
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    private EntityQuery<BloodstreamComponent> _bloodstreamQuery;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _bloodstreamQuery = GetEntityQuery<BloodstreamComponent>();

        SubscribeLocalEvent<SlaughterDemonComponent, BeingGibbedEvent>(OnGib);
    }

    private void OnGib(Entity<SlaughterDemonComponent> ent, ref BeingGibbedEvent args)
    {
        if (!TryComp<SlaughterDevourComponent>(ent.Owner, out var devour)
            || devour.Container == null)
            return;

        _container.EmptyContainer(devour.Container);

        // Allow everyone to self revive again (if they have the ability to)
        foreach (var entity in ent.Comp.ConsumedMobs)
            RemComp<PreventSelfRevivalComponent>(entity);

        // heal them if they were in the laughter demon
        if (!ent.Comp.IsLaughter)
            return;

        foreach (var entity in ent.Comp.ConsumedMobs)
            _rejuvenate.PerformRejuvenate(entity);
    }

    protected override void RemoveBlood(EntityUid uid)
    {
        base.RemoveBlood(uid);

        if (!_bloodstreamQuery.TryComp(uid, out var comp))
            return;

        _bloodstream.SpillAllSolutions((uid, comp));
    }
}
