// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Bed.Sleep;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Standing;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    private static readonly TimeSpan StakedDuration = TimeSpan.FromSeconds(60);

    [Dependency] private StandingStateSystem _standing = default!;

    private void InitializeStake()
    {
        SubscribeLocalEvent<VampireStakeComponent, AfterInteractEvent>(OnStakeInteract);
        SubscribeLocalEvent<VampireStakeComponent, VampireStakeDoAfterEvent>(OnStakeDoAfter);
    }

    private void OnStakeInteract(Entity<VampireStakeComponent> stake, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        if (!TryComp<VampireComponent>(target, out var vampireComp) || vampireComp.FinalDeath)
            return;

        args.Handled = true;

        // It only goes in while they're down - nobody holds still for this on their feet.
        if (!_standing.IsDown(target) && !_mobState.IsIncapacitated(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-stake-fail-standing", ("target", target)), args.User, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, stake.Comp.StakeDuration,
            new VampireStakeDoAfterEvent(), stake, target, stake)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _popup.PopupEntity(Loc.GetString("vampire-stake-start", ("target", target)), args.User, args.User, PopupType.MediumCaution);
    }

    private void OnStakeDoAfter(Entity<VampireStakeComponent> stake, ref VampireStakeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !TryComp<VampireComponent>(target, out var vampireComp))
            return;

        args.Handled = true;

        // Through the heart of one who can't fight back - asleep, out cold, in torpor or bled dry - it's the end.
        if (_mobState.IsIncapacitated(target) || HasComp<SleepingComponent>(target) || vampireComp.BloodPoints <= 0f)
        {
            _damageable.TryChangeDamage(target, stake.Comp.FinalDeathDamage, ignoreResistances: true, interruptsDoAfters: false);
            TriggerFinalDeath((target, vampireComp), "vampire-stake-final-death");
            return;
        }

        _popup.PopupEntity(Loc.GetString("vampire-stake-missed-heart", ("target", target)), args.User, args.User, PopupType.MediumCaution);

        var staked = EnsureComp<VampireStakedComponent>(target);
        staked.ExpiresAt = _timing.CurTime + StakedDuration * stake.Comp.StakedDurationMultiplier;
        _popup.PopupEntity(Loc.GetString("vampire-stake-disabled"), target, target, PopupType.LargeCaution);
    }

    /// <summary>
    /// True if a vampire has a stake through their heart right now - every power but Feed, and all
    /// healing, refuses to work while this holds.
    /// </summary>
    private bool IsStaked(EntityUid vampire)
    {
        return HasComp<VampireStakedComponent>(vampire);
    }

    private void UnstakeIfExpired(Entity<VampireStakedComponent> staked, TimeSpan now)
    {
        if (now >= staked.Comp.ExpiresAt)
            RemCompDeferred<VampireStakedComponent>(staked); // called while enumerating VampireStakedComponent
    }
}
