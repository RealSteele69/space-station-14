// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Shared._ClawCommand.Vampire;
using Content.Shared._ClawCommand.Vampire.Roles;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private TargetObjectiveSystem _targetObjective = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    private static readonly EntProtoId VassalRecuperateAction = "ActionVassalRecuperate";
    private static readonly EntProtoId VassalSenseMasterAction = "ActionVassalSenseMaster";
    private static readonly ProtoId<DamageGroupPrototype> VassalBruteHealGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> VassalToxinHealGroup = "Toxin";

    private void InitializeVassal()
    {
        SubscribeLocalEvent<VampireComponent, VampireEmbraceActionEvent>(OnEmbraceAction);
        SubscribeLocalEvent<VampireComponent, VampireEmbraceDoAfterEvent>(OnEmbraceDoAfter);
        SubscribeLocalEvent<VampireComponent, MobStateChangedEvent>(OnVampireMobStateChanged);
        SubscribeLocalEvent<VassalComponent, VassalRecuperateActionEvent>(OnRecuperateAction);
        SubscribeLocalEvent<VassalComponent, VassalSenseMasterActionEvent>(OnSenseMasterAction);
    }

    private void OnEmbraceAction(Entity<VampireComponent> vampire, ref VampireEmbraceActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        if (PowersBlocked(vampire)) // stake or garlic
            return;

        if (vampire.Comp.Vassals.Count >= vampire.Comp.Level)
        {
            _popup.PopupEntity(Loc.GetString("vampire-embrace-fail-no-capacity"), vampire, vampire);
            return;
        }

        if (!TryComp<MobStateComponent>(target, out var mobState)
            || HasComp<VampireComponent>(target)
            || HasComp<VassalComponent>(target)
            || HasComp<MindShieldComponent>(target)
            || !_mobState.IsIncapacitated(target, mobState))
        {
            _popup.PopupEntity(Loc.GetString("vampire-embrace-fail-target"), vampire, vampire);
            return;
        }

        var attempt = new AttemptConvertVassalEvent(false);
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("vampire-embrace-fail-target"), vampire, vampire);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, vampire.Owner, vampire.Comp.EmbraceDuration,
            new VampireEmbraceDoAfterEvent(), vampire.Owner, target)
        {
            BreakOnMove = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _popup.PopupEntity(Loc.GetString("vampire-embrace-start", ("target", target)), vampire, vampire, PopupType.MediumCaution);

        args.Handled = true;
    }

    private void OnEmbraceDoAfter(Entity<VampireComponent> vampire, ref VampireEmbraceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (vampire.Comp.Vassals.Count >= vampire.Comp.Level || HasComp<VampireComponent>(target) || HasComp<VassalComponent>(target))
            return;

        MakeVassal(vampire, target);
    }

    private void MakeVassal(Entity<VampireComponent> vampire, EntityUid target)
    {
        var vassal = EnsureComp<VassalComponent>(target);
        vassal.Master = vampire.Owner;
        vampire.Comp.Vassals.Add(target);

        if (_actions.AddAction(target, VassalRecuperateAction) is { } recuperateAction)
            vassal.ActionEntities.Add(recuperateAction);
        if (_actions.AddAction(target, VassalSenseMasterAction) is { } senseAction)
            vassal.ActionEntities.Add(senseAction);

        if (_mind.TryGetMind(target, out var mindId, out var mind))
        {
            _role.MindAddRole(mindId, "VassalMindRole");

            if (_objectives.TryCreateObjective((mindId, mind), "VassalKeepMasterAliveObjective", out var objective))
            {
                // KeepAliveCondition tracks a mind, not a body. The target is set after assignment, so the
                // generic title never got filled in; name the objective here instead.
                if (_mind.TryGetMind(vampire, out var masterMindId, out _))
                    _targetObjective.SetTarget(objective.Value, masterMindId);
                _metaData.SetEntityName(objective.Value,
                    Loc.GetString("vassal-objective-keep-master-alive-title", ("name", Name(vampire))));
                _mind.AddObjective(mindId, mind, objective.Value);
                vassal.Objective = objective.Value;
            }
        }

        _popup.PopupEntity(Loc.GetString("vampire-embrace-success-master", ("target", target)), vampire, vampire, PopupType.Large);
        _popup.PopupEntity(Loc.GetString("vampire-embrace-success-vassal", ("master", vampire.Owner)), target, target, PopupType.LargeCaution);
    }

    private void OnVampireMobStateChanged(Entity<VampireComponent> vampire, ref MobStateChangedEvent args)
    {
        if (!vampire.Comp.FinalDeath)
        {
            if (args.NewMobState == MobState.Dead)
                _popup.PopupEntity(Loc.GetString("vampire-torpor-begin"), vampire, vampire, PopupType.LargeCaution);
            else if (args.OldMobState == MobState.Dead)
                _popup.PopupEntity(Loc.GetString("vampire-torpor-end"), vampire, vampire, PopupType.Large);
        }

        if (args.NewMobState != MobState.Dead || vampire.Comp.Vassals.Count == 0)
            return;

        foreach (var vassalUid in vampire.Comp.Vassals)
            FreeVassal(vassalUid);

        vampire.Comp.Vassals.Clear();
    }

    private void FreeVassal(EntityUid vassalUid)
    {
        if (!TryComp<VassalComponent>(vassalUid, out var vassal))
            return;

        if (vassal.Objective is { } objective)
            QueueDel(objective);

        if (_mind.TryGetMind(vassalUid, out var mindId, out _))
            _role.MindRemoveRole<VassalRoleComponent>(mindId);

        foreach (var action in vassal.ActionEntities)
            _actions.RemoveAction(action);

        RemComp<VassalRecuperatingComponent>(vassalUid);
        RemComp<VassalComponent>(vassalUid);

        _popup.PopupEntity(Loc.GetString("vampire-embrace-freed"), vassalUid, vassalUid, PopupType.Large);
    }

    private void OnRecuperateAction(Entity<VassalComponent> vassal, ref VassalRecuperateActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<VassalRecuperatingComponent>(vassal, out var recuperating))
        {
            _actions.SetToggled(recuperating.Action, false);
            RemComp<VassalRecuperatingComponent>(vassal);
            args.Handled = true;
            return;
        }

        if (!TryComp<BloodstreamComponent>(vassal, out var bloodstream) || _bloodstream.GetBloodLevel((vassal.Owner, bloodstream)) < 0.3f)
        {
            _popup.PopupEntity(Loc.GetString("vassal-recuperate-fail-no-blood"), vassal, vassal);
            return;
        }

        var recuperate = EnsureComp<VassalRecuperatingComponent>(vassal);
        recuperate.Action = args.Action.Owner;
        _actions.SetToggled(args.Action.Owner, true);

        args.Handled = true;
    }

    private void OnSenseMasterAction(Entity<VassalComponent> vassal, ref VassalSenseMasterActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!Exists(vassal.Comp.Master))
        {
            _popup.PopupEntity(Loc.GetString("vassal-sense-master-gone"), vassal, vassal);
            return;
        }

        if (Transform(vassal).Coordinates.TryDistance(EntityManager, Transform(vassal.Comp.Master).Coordinates, out var distance))
            _popup.PopupEntity(Loc.GetString("vassal-sense-master-distance", ("distance", (int) distance)), vassal, vassal);
        else
            _popup.PopupEntity(Loc.GetString("vassal-sense-master-unknown"), vassal, vassal);
    }

    private void RecuperateTick(Entity<VassalRecuperatingComponent> vassal)
    {
        if (!TryComp<BloodstreamComponent>(vassal, out var bloodstream)
            || _bloodstream.GetBloodLevel((vassal.Owner, bloodstream)) < 0.3f)
        {
            _actions.SetToggled(vassal.Comp.Action, false);
            RemCompDeferred<VassalRecuperatingComponent>(vassal); // called while enumerating VassalRecuperatingComponent
            return;
        }

        _bloodstream.TryModifyBloodLevel((vassal.Owner, bloodstream), FixedPoint2.New(-2f));

        if (TryComp<DamageableComponent>(vassal, out var damageable))
        {
            var heal = new DamageSpecifier(_prototype.Index(VassalBruteHealGroup), FixedPoint2.New(-1f))
                       + new DamageSpecifier(_prototype.Index(VassalToxinHealGroup), FixedPoint2.New(-1f));
            _damageable.TryChangeDamage((vassal.Owner, damageable), heal, ignoreResistances: true, interruptsDoAfters: false);
        }
    }
}
