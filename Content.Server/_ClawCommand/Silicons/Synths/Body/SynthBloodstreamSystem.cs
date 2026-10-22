using Content.Shared._ClawCommand.Silicons.Synths.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Silicons.Synths.Body;

/// <summary>
/// Synth self-repair, paid for with hunger. See <see cref="SynthBloodstreamComponent"/>.
/// </summary>
public sealed partial class SynthBloodstreamSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SatiationSystem _satiation = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SynthBloodstreamComponent, SatiationComponent, BloodstreamComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var synth, out var satiation, out var bloodstream, out var damageable))
        {
            if (_timing.CurTime < synth.NextUpdate)
                continue;

            synth.NextUpdate = _timing.CurTime + synth.UpdateRate;

            if (_mobState.IsDead(uid))
                continue;

            UpdateSynthBloodstream((uid, synth, satiation, bloodstream, damageable));
        }
    }

    private void UpdateSynthBloodstream(Entity<SynthBloodstreamComponent, SatiationComponent, BloodstreamComponent, DamageableComponent> ent)
    {
        if (_satiation.GetValueOrNull((ent.Owner, ent.Comp2), SatiationSystem.Hunger) is not { } hunger
            || hunger <= ent.Comp1.MinHunger)
            return;

        var availableHunger = hunger - ent.Comp1.MinHunger;
        var hungerCost = 0f;

        var efficiency = GetBloodEfficiency(ent.Comp1, _bloodstream.GetBloodLevel((ent.Owner, ent.Comp3)));
        if (efficiency > 0f)
        {
            hungerCost += Repair(ent, efficiency, availableHunger);
            hungerCost += SealBleeding(ent, efficiency, MathF.Max(0f, availableHunger - hungerCost));
        }

        hungerCost += RegrowBlood(ent, MathF.Max(0f, availableHunger - hungerCost));

        if (hungerCost > 0f)
            _satiation.ModifyValue((ent.Owner, ent.Comp2), SatiationSystem.Hunger, -MathF.Min(hungerCost, availableHunger));
    }

    /// <summary>
    /// Heals the configured damage, scaled by blood efficiency and limited by the hunger on hand.
    /// Returns the hunger spent.
    /// </summary>
    private float Repair(
        Entity<SynthBloodstreamComponent, SatiationComponent, BloodstreamComponent, DamageableComponent> ent,
        float efficiency,
        float availableHunger)
    {
        var comp = ent.Comp1;
        var damage = _damageable.GetPositiveDamage((ent.Owner, ent.Comp4));
        if (damage.Empty)
            return 0f;

        // Work out how much would be healed this tick, capped by the damage actually there.
        var groups = new List<(ProtoId<DamageGroupPrototype> Group, FixedPoint2 Amount)>();
        var types = new List<(ProtoId<DamageTypePrototype> Type, FixedPoint2 Amount)>();
        var total = FixedPoint2.Zero;

        foreach (var (groupId, amount) in comp.Damage.Groups)
        {
            if (!_prototype.TryIndex(groupId, out var group))
                continue;

            var groupDamage = FixedPoint2.Zero;
            foreach (var type in group.DamageTypes)
            {
                groupDamage += damage.DamageDict.GetValueOrDefault(type);
            }

            var heal = FixedPoint2.Min(-amount * efficiency, groupDamage);
            if (heal <= FixedPoint2.Zero)
                continue;

            groups.Add((groupId, heal));
            total += heal;
        }

        foreach (var (type, amount) in comp.Damage.Types)
        {
            var heal = FixedPoint2.Min(-amount * efficiency, damage.DamageDict.GetValueOrDefault(type));
            if (heal <= FixedPoint2.Zero)
                continue;

            types.Add((type, heal));
            total += heal;
        }

        if (total <= FixedPoint2.Zero)
            return 0f;

        var scale = 1f;
        if (comp.HungerCostPerRepair > 0f)
            scale = MathF.Min(1f, availableHunger / comp.HungerCostPerRepair / total.Float());

        if (scale <= 0f)
            return 0f;

        var healed = FixedPoint2.Zero;

        foreach (var (group, heal) in groups)
        {
            healed -= _damageable.HealDistributed((ent.Owner, ent.Comp4), -heal * scale, group).GetTotal();
        }

        if (types.Count > 0)
        {
            var typeRepair = new DamageSpecifier();
            foreach (var (type, heal) in types)
            {
                typeRepair.DamageDict[type] = -heal * scale;
            }

            if (_damageable.TryChangeDamage((ent.Owner, ent.Comp4), typeRepair, out var repaired, ignoreResistances: true, interruptsDoAfters: false))
                healed -= repaired.GetTotal();
        }

        return MathF.Max(0f, healed.Float()) * comp.HungerCostPerRepair;
    }

    /// <summary>
    /// Closes up bleeding wounds. Returns the hunger spent.
    /// </summary>
    private float SealBleeding(
        Entity<SynthBloodstreamComponent, SatiationComponent, BloodstreamComponent, DamageableComponent> ent,
        float efficiency,
        float availableHunger)
    {
        var comp = ent.Comp1;
        if (comp.BleedReductionAmount <= 0f || ent.Comp3.BleedAmount <= 0f || availableHunger <= 0f)
            return 0f;

        var sealedAmount = MathF.Min(ent.Comp3.BleedAmount, comp.BleedReductionAmount * efficiency);
        if (comp.HungerCostPerBleed > 0f)
            sealedAmount = MathF.Min(sealedAmount, availableHunger / comp.HungerCostPerBleed);

        if (sealedAmount <= 0f || !_bloodstream.TryModifyBleedAmount((ent.Owner, ent.Comp3), -sealedAmount))
            return 0f;

        return sealedAmount * comp.HungerCostPerBleed;
    }

    /// <summary>
    /// Regrows blood up to the target level. Returns the hunger spent.
    /// </summary>
    private float RegrowBlood(
        Entity<SynthBloodstreamComponent, SatiationComponent, BloodstreamComponent, DamageableComponent> ent,
        float availableHunger)
    {
        var regeneration = ent.Comp1.BloodRegeneration;
        if (_bloodstream.GetBloodLevel((ent.Owner, ent.Comp3)) >= regeneration.TargetBloodLevel)
            return 0f;

        var amount = regeneration.BloodRefreshAmount;
        if (regeneration.HungerCostPerUnit > 0f)
            amount = FixedPoint2.Min(amount, FixedPoint2.New(availableHunger / regeneration.HungerCostPerUnit));

        if (amount <= FixedPoint2.Zero || !_bloodstream.TryModifyBloodLevel((ent.Owner, ent.Comp3), amount))
            return 0f;

        return amount.Float() * regeneration.HungerCostPerUnit;
    }

    private static float GetBloodEfficiency(SynthBloodstreamComponent component, float bloodLevel)
    {
        if (bloodLevel < component.MinBloodLevel)
            return 0f;

        if (bloodLevel >= component.FullEfficiencyBloodLevel || component.FullEfficiencyBloodLevel <= component.MinBloodLevel)
            return 1f;

        var progress = (bloodLevel - component.MinBloodLevel) / (component.FullEfficiencyBloodLevel - component.MinBloodLevel);
        return MathHelper.Lerp(component.MinBloodEfficiency, 1f, Math.Clamp(progress, 0f, 1f));
    }
}
