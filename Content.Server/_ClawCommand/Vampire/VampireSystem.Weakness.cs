// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Body;
using Content.Shared.Cloning.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Vampire;

/// <summary>
/// How vampires are beaten. Death only sends them into torpor, where they keep healing and get back up;
/// final death needs a stake while they're helpless, enough fire, or losing their brain or heart. Garlic
/// (eaten or worn) shuts off their healing and powers, and they can never be cloned.
/// </summary>
public sealed partial class VampireSystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    private static readonly ProtoId<DamageGroupPrototype> FinalDeathBurnGroup = "Burn";
    private static readonly ProtoId<DamageGroupPrototype> TorporAirlossGroup = "Airloss";
    private const string BrainCategory = "Brain";
    private const string HeartCategory = "Heart";
    private const string AllicinReagent = "Allicin";
    private const string NeckSlot = "neck";

    private void InitializeWeakness()
    {
        SubscribeLocalEvent<VampireComponent, CloningAttemptEvent>(OnCloningAttempt);
        SubscribeLocalEvent<VampireComponent, IngestingEvent>(OnIngesting);
    }

    /// <summary>
    /// Called once the vampire exists: death becomes torpor, and the organs it has now are the ones it
    /// must keep.
    /// </summary>
    private void SetupWeakness(Entity<VampireComponent> vampire)
    {
        _mobThreshold.SetAllowRevives(vampire, true);

        foreach (var organ in _body.EnumerateOrgans<TransformComponent>(vampire.Owner))
        {
            if (organ.Comp1.Category?.Id == BrainCategory)
                vampire.Comp.HadBrain = true;
            else if (organ.Comp1.Category?.Id == HeartCategory)
                vampire.Comp.HadHeart = true;
        }
    }

    private void OnCloningAttempt(Entity<VampireComponent> vampire, ref CloningAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnIngesting(Entity<VampireComponent> vampire, ref IngestingEvent args)
    {
        var allicin = args.Split.GetTotalPrototypeQuantity(AllicinReagent);
        if (allicin <= 0)
            return;

        var now = _timing.CurTime;
        var start = vampire.Comp.GarlicUntil > now ? vampire.Comp.GarlicUntil : now;
        vampire.Comp.GarlicUntil = start + TimeSpan.FromSeconds(allicin.Float() * vampire.Comp.GarlicSecondsPerUnit);
        _popup.PopupEntity(Loc.GetString("vampire-garlic-ingested"), vampire, vampire, PopupType.MediumCaution);
    }

    /// <summary>
    /// Garlic in the blood or around the neck.
    /// </summary>
    public bool IsGarlicked(Entity<VampireComponent> vampire)
    {
        if (vampire.Comp.GarlicUntil > _timing.CurTime)
            return true;

        return _inventory.TryGetSlotEntity(vampire, NeckSlot, out var neck)
               && HasComp<VampireGarlicWardComponent>(neck);
    }

    /// <summary>
    /// True (and tells the vampire why) if a stake or garlic is stopping their powers right now.
    /// </summary>
    private bool PowersBlocked(Entity<VampireComponent> vampire, bool ignoreStake = false)
    {
        if (!ignoreStake && IsStaked(vampire))
        {
            _popup.PopupEntity(Loc.GetString("vampire-power-fail-staked"), vampire, vampire);
            return true;
        }

        if (!IsGarlicked(vampire))
            return false;

        var reason = vampire.Comp.GarlicUntil > _timing.CurTime ? "vampire-power-fail-garlic" : "vampire-power-fail-garlic-necklace";
        _popup.PopupEntity(Loc.GetString(reason), vampire, vampire);
        return true;
    }

    /// <summary>
    /// Fire and missing organs take a vampire past torpor. Checked every life tick, in any state.
    /// </summary>
    private void CheckFinalDeath(Entity<VampireComponent> vampire)
    {
        if (vampire.Comp.FinalDeath)
            return;

        if (_damageable.GetDamagePerGroup(vampire.Owner).TryGetValue(FinalDeathBurnGroup, out var burn)
            && burn >= vampire.Comp.FinalDeathBurn)
        {
            TriggerFinalDeath(vampire, "vampire-final-death-fire");
            return;
        }

        if (!vampire.Comp.HadBrain && !vampire.Comp.HadHeart)
            return;

        var hasBrain = false;
        var hasHeart = false;
        foreach (var organ in _body.EnumerateOrgans<TransformComponent>(vampire.Owner))
        {
            if (organ.Comp1.Category?.Id == BrainCategory)
                hasBrain = true;
            else if (organ.Comp1.Category?.Id == HeartCategory)
                hasHeart = true;
        }

        if ((vampire.Comp.HadBrain && !hasBrain) || (vampire.Comp.HadHeart && !hasHeart))
            TriggerFinalDeath(vampire, "vampire-final-death-organs");
    }

    /// <summary>
    /// Ends a vampire for good: no more torpor, no defibrillator, no cloning.
    /// </summary>
    public void TriggerFinalDeath(Entity<VampireComponent> vampire, string messageLoc)
    {
        if (vampire.Comp.FinalDeath)
            return;

        vampire.Comp.FinalDeath = true;
        _mobThreshold.SetAllowRevives(vampire, false);
        EnsureComp<UnrevivableComponent>(vampire); // blocks defibrillators and cloning

        if (!_mobState.IsDead(vampire))
            _mobState.ChangeMobState(vampire, MobState.Dead);

        _popup.PopupEntity(Loc.GetString(messageLoc, ("target", vampire.Owner)), vampire, PopupType.LargeCaution);
    }

    /// <summary>
    /// A dead vampire's body keeps knitting itself back together - burns included - so long as Sol
    /// isn't up and nothing is stopping it. Once healed enough, it simply gets back up.
    /// </summary>
    private void TorporTick(Entity<VampireComponent> vampire)
    {
        if (_solActive || HasComp<VampireMasqueradeComponent>(vampire))
            return;

        Regenerate(vampire, vampire.Comp.TorporRegenMultiplier, healBurn: true);

        // Bled out or suffocated: that has to mend too, or they would never get back up.
        if (!IsStaked(vampire) && !IsGarlicked(vampire))
        {
            var amount = FixedPoint2.New(-vampire.Comp.RegenRate * vampire.Comp.TorporRegenMultiplier);
            _damageable.TryChangeDamage(vampire.Owner, new DamageSpecifier(_prototype.Index(TorporAirlossGroup), amount),
                ignoreResistances: true, interruptsDoAfters: false);
        }
    }
}
