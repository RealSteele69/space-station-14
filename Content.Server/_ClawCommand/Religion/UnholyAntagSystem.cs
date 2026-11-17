// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Religion;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared._ClawCommand.CosmicCult.Components;
using Content.Shared.Heretic;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Religion;

/// <summary>
/// Marks an entity that <see cref="UnholyAntagSystem"/> made weak to holy damage, and what to restore afterwards.
/// </summary>
[RegisterComponent, Access(typeof(UnholyAntagSystem))]
public sealed partial class UnholyAntagComponent : Component
{
    /// <summary>
    /// Whether it already had <see cref="WeakToHolyComponent"/> from something else (devil, ghoul...), so it stays.
    /// </summary>
    [ViewVariables]
    public bool HadWeakness;

    /// <summary>
    /// The pre-existing <see cref="WeakToHolyComponent.AlwaysTakeHoly"/> value to restore.
    /// </summary>
    [ViewVariables]
    public bool PreviousAlwaysTakeHoly;
}

/// <summary>
/// Blood cultists, cosmic cultists and heretics take holy damage for as long as they serve their patron.
/// Deconverting them (holy water, the censer...) lifts it again.
/// </summary>
/// <remarks>
/// Heretic status lives on the mind and cult status can change in many places, so this reconciles periodically
/// instead of hooking every conversion path.
/// </remarks>
public sealed partial class UnholyAntagSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    private TimeSpan _nextUpdate;
    private readonly HashSet<EntityUid> _unholy = new();
    private readonly List<EntityUid> _toRestore = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        _unholy.Clear();

        var bloodQuery = EntityQueryEnumerator<BloodCultistComponent, MobStateComponent>();
        while (bloodQuery.MoveNext(out var uid, out _, out _))
        {
            _unholy.Add(uid);
        }

        var cosmicQuery = EntityQueryEnumerator<CosmicCultComponent, MobStateComponent>();
        while (cosmicQuery.MoveNext(out var uid, out _, out _))
        {
            _unholy.Add(uid);
        }

        var hereticQuery = EntityQueryEnumerator<HereticComponent, MindComponent>();
        while (hereticQuery.MoveNext(out _, out _, out var mind))
        {
            if (mind.OwnedEntity is { } body && HasComp<MobStateComponent>(body))
                _unholy.Add(body);
        }

        foreach (var uid in _unholy)
        {
            if (!HasComp<UnholyAntagComponent>(uid))
                MakeUnholy(uid);
        }

        _toRestore.Clear();
        var markedQuery = EntityQueryEnumerator<UnholyAntagComponent>();
        while (markedQuery.MoveNext(out var uid, out _))
        {
            if (!_unholy.Contains(uid))
                _toRestore.Add(uid);
        }

        foreach (var uid in _toRestore)
        {
            Restore(uid);
        }
    }

    private void MakeUnholy(EntityUid uid)
    {
        var marker = AddComp<UnholyAntagComponent>(uid);

        if (TryComp<WeakToHolyComponent>(uid, out var existing))
        {
            marker.HadWeakness = true;
            marker.PreviousAlwaysTakeHoly = existing.AlwaysTakeHoly;
            existing.AlwaysTakeHoly = true;
            Dirty(uid, existing);
            return;
        }

        var weak = AddComp<WeakToHolyComponent>(uid);
        weak.AlwaysTakeHoly = true;
        Dirty(uid, weak);
    }

    private void Restore(EntityUid uid)
    {
        if (!TryComp<UnholyAntagComponent>(uid, out var marker))
            return;

        if (!marker.HadWeakness)
        {
            RemComp<WeakToHolyComponent>(uid);
        }
        else if (TryComp<WeakToHolyComponent>(uid, out var weak))
        {
            weak.AlwaysTakeHoly = marker.PreviousAlwaysTakeHoly;
            Dirty(uid, weak);
        }

        RemComp(uid, marker);
    }
}
