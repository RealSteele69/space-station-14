// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    private void InitializeMesmerize()
    {
        SubscribeLocalEvent<VampireComponent, VampireMesmerizeActionEvent>(OnMesmerizeAction);
        SubscribeLocalEvent<VampireComponent, VampireMesmerizeDoAfterEvent>(OnMesmerizeDoAfter);
    }

    private void OnMesmerizeAction(Entity<VampireComponent> vampire, ref VampireMesmerizeActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        if (!HasComp<MobStateComponent>(target) || HasComp<VampireComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-mesmerize-fail-target"), vampire, vampire);
            return;
        }

        if (PowersBlocked(vampire)) // stake or garlic
            return;

        if (vampire.Comp.BloodPoints < vampire.Comp.MesmerizeActivationCost)
        {
            _popup.PopupEntity(Loc.GetString("vampire-mesmerize-fail-no-blood"), vampire, vampire);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, vampire.Owner, vampire.Comp.MesmerizeWindup,
            new VampireMesmerizeDoAfterEvent(), vampire.Owner, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _popup.PopupEntity(Loc.GetString("vampire-mesmerize-start", ("target", target)), vampire, vampire);

        args.Handled = true;
    }

    private void OnMesmerizeDoAfter(Entity<VampireComponent> vampire, ref VampireMesmerizeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (vampire.Comp.BloodPoints < vampire.Comp.MesmerizeActivationCost)
            return;

        AddBloodPoints(vampire, -vampire.Comp.MesmerizeActivationCost);
        _stun.TryUpdateParalyzeDuration(target, vampire.Comp.MesmerizeStunDuration);
        _popup.PopupEntity(Loc.GetString("vampire-mesmerize-success"), target, target);
    }
}
