// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source granted/cleared SlasherVictimFearBuildupComponent through a wrapping
// "StatusEffectFeared" status effect entity (StatusEffectsSystem.TryAddStatusEffect/
// TryRemoveStatusEffect + GrantComponentsStatusEffect). GrantComponentsStatusEffect is a dead,
// data-only stub on this fork (no system reads it), so this port adds/removes
// SlasherVictimFearBuildupComponent directly instead of going through a status effect at all.

using System.Diagnostics.CodeAnalysis;
using Content.Shared._GoobStation.Slasher.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Systems;

/// <summary>
/// The victim side of the fear system.
/// </summary>
public sealed partial class SlasherFearSystem
{
    private void OnFearedDetached(Entity<SlasherVictimFearBuildupComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        FadeVictimMusic(ent);
    }

    private void OnFearedShutdown(Entity<SlasherVictimFearBuildupComponent> ent, ref ComponentShutdown args)
    {
        FadeVictimMusic(ent);

        if (_net.IsServer)
            ClearFearStyle(ent);
    }

    private void UpdateVictimFearBuildup(TimeSpan now)
    {
        var feared = EntityQueryEnumerator<SlasherVictimFearBuildupComponent>();
        while (feared.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateInterval;
            UpdateVictim((uid, comp), now);
        }
    }

    /// <summary>
    /// Marks a victim as seen by a slasher. Grants SlasherVictimFearBuildupComponent if they don't
    /// already have it.
    /// </summary>
    /// <returns>False if the victim belongs to another slasher.</returns>
    private bool TryObserveVictim(Entity<SlasherFearComponent> slasher,
        EntityUid other,
        TimeSpan now,
        [NotNullWhen(true)] out SlasherVictimFearBuildupComponent? victim)
    {
        var (uid, comp) = slasher;

        var buildup = EnsureComp<SlasherVictimFearBuildupComponent>(other);

        var netUid = GetNetEntity(uid);

        // This stops evil things from happening if there's more than 1 slasher.
        if (GetEntity(buildup.Scarer) is { } owner
            && owner != uid
            && !TerminatingOrDeleted(owner)
            && HasComp<SlasherFearComponent>(owner)
            && now - buildup.LastObserved < buildup.OwnershipTimeout)
        {
            victim = null;
            return false;
        }

        if (buildup.Scarer != netUid)
        {
            buildup.Scarer = netUid;
            if (_net.IsServer)
                ApplyFearStyle((other, buildup), comp.GrantedToVictimOnSight);
        }

        buildup.LastObserved = now;
        victim = buildup;
        return true;
    }

    private void ReleaseVictims(EntityUid slasher)
    {
        var netSlasher = GetNetEntity(slasher);
        var victims = EntityQueryEnumerator<SlasherVictimFearBuildupComponent>();
        while (victims.MoveNext(out var victimUid, out var victim))
        {
            if (victim.Scarer != netSlasher)
                continue;

            victim.Scarer = null;
            Dirty(victimUid, victim);
        }
    }

    private void UpdateVictim(Entity<SlasherVictimFearBuildupComponent> ent, TimeSpan now)
    {
        var (uid, comp) = ent;

        var sinceObserved = now - comp.LastObserved;
        var observed = sinceObserved < comp.ObserveTimeout;

        if (observed)
            comp.Fear = MathF.Min(1f, comp.Fear + comp.GainPerSecond);
        else if (sinceObserved >= comp.FearGracePeriod)
            comp.Fear = MathF.Max(0f, comp.Fear - comp.DecayPerSecond);

        if (comp.Fear <= 0f && !observed)
        {
            RemCompDeferred<SlasherVictimFearBuildupComponent>(uid);
            return;
        }

        if (comp.Fear >= comp.SlowThreshold)
            _movementMod.TryUpdateMovementSpeedModDuration(
                uid, comp.SlowEffect, comp.SlowRefresh, comp.SlowMultiplier, comp.SlowMultiplier);

        if (comp.Fear >= comp.DamageThreshold && _net.IsServer)
            _damageable.TryChangeDamage(uid, comp.DamagePerSecond, true);

        Dirty(uid, comp);
        UpdateVictimMusic(ent);
    }

    private void ApplyFearStyle(Entity<SlasherVictimFearBuildupComponent> victim, ComponentRegistry style)
    {
        ClearFearStyle(victim);

        EntityManager.AddComponents(victim.Owner, style);
        victim.Comp.AppliedStyle = style;
    }

    private void ClearFearStyle(Entity<SlasherVictimFearBuildupComponent> victim)
    {
        if (victim.Comp.AppliedStyle is not { } style)
            return;

        EntityManager.RemoveComponents(victim.Owner, style);
        victim.Comp.AppliedStyle = null;
    }
}
