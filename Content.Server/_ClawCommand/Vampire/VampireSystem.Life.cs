// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;

    /// <summary>
    /// Runs once per <see cref="VampireComponent.TickInterval"/> for every vampire: Feed drain if latched
    /// onto a victim, otherwise passive blood drain, blood-gated regeneration, and starvation once dry.
    /// </summary>
    private void LifeTick(Entity<VampireComponent> vampire)
    {
        if (vampire.Comp.FinalDeath)
            return;

        ProcessSunlight(vampire);

        if (TryComp<VampireFeedingComponent>(vampire, out var feeding))
        {
            ProcessFeeding(vampire, (vampire.Owner, feeding));
            return;
        }

        if (!_mobState.IsAlive(vampire) && !_mobState.IsCritical(vampire))
            return; // Dead vampires neither drain nor regenerate - final death handling is a later pass.

        // Resting in your own closed coffin is free: no blood spent, and wounds close far faster.
        if (IsRestingInOwnCoffin(vampire))
        {
            Regenerate(vampire, vampire.Comp.CoffinRegenMultiplier, healBurn: true);
            return;
        }

        var drain = vampire.Comp.PassiveBloodDrain;
        if (HasComp<VampireMasqueradeComponent>(vampire))
            drain += vampire.Comp.MasqueradeDrainRate;
        if (HasComp<VampireCloakComponent>(vampire))
            drain += vampire.Comp.CloakDrainRate;
        if (_status.HasStatusEffect(vampire, vampire.Comp.FortitudeStatusEffect))
            drain += vampire.Comp.FortitudeDrainRate;

        AddBloodPoints(vampire, -drain);

        if (vampire.Comp.BloodPoints <= 0f)
        {
            _jittering.DoJitter(vampire, TimeSpan.FromSeconds(1), true);
            return;
        }

        // Masquerading fakes being alive convincingly enough that it stops wounds from visibly closing.
        if (HasComp<VampireMasqueradeComponent>(vampire))
            return;

        Regenerate(vampire);
    }

    private void Regenerate(Entity<VampireComponent> vampire, float multiplier = 1f, bool healBurn = false)
    {
        if (IsStaked(vampire) || !TryComp<DamageableComponent>(vampire, out var damageable))
            return;

        var amount = FixedPoint2.New(-vampire.Comp.RegenRate * multiplier);
        var heal = new DamageSpecifier(_prototype.Index(vampire.Comp.BruteHealGroup), amount)
                   + new DamageSpecifier(_prototype.Index(vampire.Comp.ToxinHealGroup), amount);

        if (healBurn)
            heal += new DamageSpecifier(_prototype.Index(vampire.Comp.BurnHealGroup), amount);

        _damageable.TryChangeDamage((vampire.Owner, damageable), heal, ignoreResistances: true, interruptsDoAfters: false);
    }
}
