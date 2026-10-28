// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private IRobustRandom _random = default!;

    private void InitializeRankUp()
    {
        SubscribeLocalEvent<VampireComponent, GetVerbsEvent<Verb>>(AddRankUpVerbs);
    }

    /// <summary>
    /// Resting in your own closed coffin with a rank saved up and the blood to spend it is how a vampire
    /// grows in power - offered as self-verbs so picking a power needs no new UI.
    /// </summary>
    private void AddRankUpVerbs(Entity<VampireComponent> vampire, ref GetVerbsEvent<Verb> args)
    {
        if (args.User != args.Target || !args.CanInteract)
            return;

        if (vampire.Comp.LevelUnspent <= 0 || !IsRestingInOwnCoffin(vampire))
            return;

        var cost = vampire.Comp.MaxBloodPoints * vampire.Comp.LevelCostFraction;
        if (vampire.Comp.BloodPoints < cost)
            return;

        var unowned = new List<EntProtoId>();
        foreach (var power in vampire.Comp.PurchasablePowers)
        {
            if (!vampire.Comp.PurchasedPowers.Contains(power))
                unowned.Add(power);
        }

        if (unowned.Count == 0)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("vampire-rankup-verb-stats"),
                Act = () => RankUp(vampire, null, cost),
            });
            return;
        }

        foreach (var power in unowned)
        {
            var chosen = power;
            var powerName = _prototype.Index<EntityPrototype>(chosen).Name;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("vampire-rankup-verb-power", ("power", powerName)),
                Act = () => RankUp(vampire, chosen, cost),
            });
        }
    }

    /// <summary>
    /// Surviving a Sol ages every vampire by a rank. It's spent by resting in their own coffin.
    /// </summary>
    private void RankUpAll()
    {
        var query = EntityQueryEnumerator<VampireComponent>();
        while (query.MoveNext(out var uid, out var vampire))
        {
            if (vampire.FinalDeath)
                continue;

            vampire.LevelUnspent++;

            var message = IsRestingInOwnCoffin((uid, vampire))
                ? "vampire-rankup-gained-coffin"
                : vampire.ClaimedCoffin == null && vampire.LevelUnspent >= 2
                    ? "vampire-rankup-gained-no-coffin"
                    : "vampire-rankup-gained";
            Notify(uid, Loc.GetString(message), null);
        }
    }

    private void RankUp(Entity<VampireComponent> vampire, EntProtoId? power, float cost)
    {
        // The verb's Act runs later than the menu was built, so re-check everything is still true.
        if (vampire.Comp.LevelUnspent <= 0 || vampire.Comp.BloodPoints < cost || !IsRestingInOwnCoffin(vampire))
            return;

        AddBloodPoints(vampire, -cost);

        if (power is { } chosen && vampire.Comp.PurchasedPowers.Add(chosen) && _actions.AddAction(vampire, chosen) is { } action)
            vampire.Comp.ActionEntities?.Add(action);

        vampire.Comp.RegenRate += vampire.Comp.RegenRateGrowth;
        vampire.Comp.FeedAmount += vampire.Comp.FeedAmountGrowth;
        vampire.Comp.MaxBloodPoints += vampire.Comp.MaxBloodPointsGrowth;

        vampire.Comp.Level++;
        vampire.Comp.LevelUnspent--;

        if (vampire.Comp.VampireTitle == null)
        {
            var titles = _prototype.Index(vampire.Comp.IsMale ? vampire.Comp.TitleMale : vampire.Comp.TitleFemale);
            vampire.Comp.VampireTitle = _random.Pick(titles.Values);
        }

        if (vampire.Comp.Level == vampire.Comp.MasterLevel)
        {
            var reputations = _prototype.Index(vampire.Comp.ReputationMaster);
            vampire.Comp.VampireReputation = _random.Pick(reputations.Values);
        }

        UpdateBloodAlert(vampire);
        _popup.PopupEntity(Loc.GetString("vampire-rankup-success", ("level", vampire.Comp.Level)), vampire, vampire);
    }
}
