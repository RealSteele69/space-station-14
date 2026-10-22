// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._ClawCommand.Devil.DelayedDeath;
using Content.Shared._ClawCommand.CheatDeath;
using Content.Shared.Actions;
using Content.Shared.Administration.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Devil.CheatDeath;

public sealed partial class CheatDeathSystem : EntitySystem
{
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobThresholdSystem _threshold = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly ProtoId<DamageTypePrototype> Holy = "Holy";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CheatDeathComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<CheatDeathComponent, ComponentRemove>(OnRemoval);
        SubscribeLocalEvent<CheatDeathComponent, CheatDeathEvent>(OnDeathCheatAttempt);
        SubscribeLocalEvent<CheatDeathComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CheatDeathComponent, DelayedDeathEvent>(OnDelayedDeath);
    }

    private void OnInit(Entity<CheatDeathComponent> ent, ref MapInitEvent args) =>
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.ActionCheatDeath);

    private void OnRemoval(Entity<CheatDeathComponent> ent, ref ComponentRemove args) =>
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

    private void OnExamined(Entity<CheatDeathComponent> ent, ref ExaminedEvent args)
    {
        if (args.Examined != args.Examiner)
            return;

        if (ent.Comp.InfiniteRevives)
        {
            args.PushMarkup(Loc.GetString("cheat-death-component-remaining-revives-unlimited"));
            return;
        }

        args.PushMarkup(Loc.GetString("cheat-death-component-remaining-revives", ("amount", ent.Comp.ReviveAmount)));
    }

    private void OnDelayedDeath(Entity<CheatDeathComponent> ent, ref DelayedDeathEvent args)
    {
        if (args.PreventRevive)
            RemCompDeferred(ent.Owner, ent.Comp);
    }

    private void OnDeathCheatAttempt(Entity<CheatDeathComponent> ent, ref CheatDeathEvent args)
    {
        if (args.Handled)
            return;

        if (!_mobState.IsDead(ent) && !ent.Comp.CanCheatStanding)
        {
            _popup.PopupEntity(Loc.GetString("action-cheat-death-fail-not-dead"), ent, ent, PopupType.LargeCaution);
            return;
        }

        // If the entity is out of revives, or if they are unrevivable, return.
        if (ent.Comp.ReviveAmount <= 0 && !ent.Comp.InfiniteRevives || HasComp<UnrevivableComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("action-cheat-death-fail-no-lives"), ent, ent, PopupType.LargeCaution);
            return;
        }

        // If the holy damage exceeds the crit state, do not allow revives.
        if (TryComp<DamageableComponent>(ent, out var damageable)
            && _threshold.TryGetIncapThreshold(ent, out var incapThreshold)
            && _damageable.GetAllDamage((ent, damageable)).DamageDict.GetValueOrDefault(Holy) >= incapThreshold)
        {
            _popup.PopupEntity(Loc.GetString("action-cheat-death-holy-damage"), ent, ent, PopupType.LargeCaution);
            return;
        }

        var popup = _mobState.IsDead(ent)
            ? Loc.GetString("action-cheated-death-dead", ("name", Name(ent)))
            : Loc.GetString("action-cheated-death-alive", ("name", Name(ent)));
        _popup.PopupEntity(popup, ent, PopupType.LargeCaution);

        // Revive entity
        _rejuvenate.PerformRejuvenate(ent);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(5), true);

        if (!ent.Comp.InfiniteRevives)
        {
            ent.Comp.ReviveAmount--;

            if (ent.Comp.ReviveAmount <= 0)
                RemCompDeferred(ent.Owner, ent.Comp);
        }

        args.Handled = true;
    }
}
