// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.StatusEffectNew;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private StatusEffectsSystem _status = default!;

    private void InitializeFortitude()
    {
        SubscribeLocalEvent<VampireComponent, VampireFortitudeActionEvent>(OnFortitudeAction);
    }

    private void OnFortitudeAction(Entity<VampireComponent> vampire, ref VampireFortitudeActionEvent args)
    {
        if (args.Handled)
            return;

        if (_status.HasStatusEffect(vampire, vampire.Comp.FortitudeStatusEffect))
        {
            _status.TryRemoveStatusEffect(vampire, vampire.Comp.FortitudeStatusEffect);
            _actions.SetToggled(args.Action.Owner, false);
            _popup.PopupEntity(Loc.GetString("vampire-fortitude-off"), vampire, vampire);
            args.Handled = true;
            return;
        }

        if (PowersBlocked(vampire)) // stake or garlic
            return;

        if (vampire.Comp.BloodPoints < vampire.Comp.FortitudeActivationCost)
        {
            _popup.PopupEntity(Loc.GetString("vampire-fortitude-fail-no-blood"), vampire, vampire);
            return;
        }

        AddBloodPoints(vampire, -vampire.Comp.FortitudeActivationCost);
        _status.TrySetStatusEffectDuration(vampire, vampire.Comp.FortitudeStatusEffect, duration: null);
        _actions.SetToggled(args.Action.Owner, true);
        _popup.PopupEntity(Loc.GetString("vampire-fortitude-on"), vampire, vampire);

        args.Handled = true;
    }
}
