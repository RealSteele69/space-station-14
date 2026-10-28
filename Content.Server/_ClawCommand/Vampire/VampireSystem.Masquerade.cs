// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Popups;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    private void InitializeMasquerade()
    {
        SubscribeLocalEvent<VampireComponent, VampireMasqueradeActionEvent>(OnMasqueradeAction);
    }

    private void OnMasqueradeAction(Entity<VampireComponent> vampire, ref VampireMasqueradeActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<VampireMasqueradeComponent>(vampire))
        {
            RemComp<VampireMasqueradeComponent>(vampire);
            _actions.SetToggled(args.Action.Owner, false);
            _popup.PopupEntity(Loc.GetString("vampire-masquerade-off"), vampire, vampire);
            args.Handled = true;
            return;
        }

        if (IsStaked(vampire))
        {
            _popup.PopupEntity(Loc.GetString("vampire-power-fail-staked"), vampire, vampire);
            return;
        }

        if (vampire.Comp.BloodPoints < vampire.Comp.MasqueradeActivationCost)
        {
            _popup.PopupEntity(Loc.GetString("vampire-masquerade-fail-no-blood"), vampire, vampire);
            return;
        }

        AddBloodPoints(vampire, -vampire.Comp.MasqueradeActivationCost);
        EnsureComp<VampireMasqueradeComponent>(vampire);
        _actions.SetToggled(args.Action.Owner, true);
        _popup.PopupEntity(Loc.GetString("vampire-masquerade-on"), vampire, vampire, PopupType.Medium);

        args.Handled = true;
    }
}
