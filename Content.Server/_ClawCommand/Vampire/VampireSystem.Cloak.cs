// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Damage.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private SharedStealthSystem _stealth = default!;

    private void InitializeCloak()
    {
        SubscribeLocalEvent<VampireComponent, VampireCloakActionEvent>(OnCloakAction);
        SubscribeLocalEvent<VampireCloakComponent, DamageDealtEvent>(OnCloakDamaged);
    }

    private void OnCloakAction(Entity<VampireComponent> vampire, ref VampireCloakActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<VampireCloakComponent>(vampire, out var cloak))
        {
            StopCloak(vampire, (vampire.Owner, cloak));
            args.Handled = true;
            return;
        }

        if (PowersBlocked(vampire)) // stake or garlic
            return;

        if (vampire.Comp.BloodPoints < vampire.Comp.CloakActivationCost)
        {
            _popup.PopupEntity(Loc.GetString("vampire-cloak-fail-no-blood"), vampire, vampire);
            return;
        }

        AddBloodPoints(vampire, -vampire.Comp.CloakActivationCost);

        var cloakComp = EnsureComp<VampireCloakComponent>(vampire);
        cloakComp.Action = args.Action.Owner;

        var stealth = EnsureComp<StealthComponent>(vampire);
        _stealth.SetVisibility(vampire, vampire.Comp.CloakVisibility, stealth);

        _actions.SetToggled(args.Action.Owner, true);
        _popup.PopupEntity(Loc.GetString("vampire-cloak-on"), vampire, vampire);

        args.Handled = true;
    }

    private void OnCloakDamaged(Entity<VampireCloakComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Damage.GetTotal() <= 0 || !TryComp<VampireComponent>(ent, out var vampire))
            return;

        StopCloak(new Entity<VampireComponent>(ent.Owner, vampire), ent);
    }

    private void StopCloak(Entity<VampireComponent> vampire, Entity<VampireCloakComponent> cloak)
    {
        RemComp<StealthComponent>(vampire);
        _actions.SetToggled(cloak.Comp.Action, false);
        RemComp<VampireCloakComponent>(vampire);
        _popup.PopupEntity(Loc.GetString("vampire-cloak-off"), vampire, vampire);
    }
}
