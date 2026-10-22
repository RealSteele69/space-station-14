// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Text.RegularExpressions;
using Content.Server._ClawCommand.Devil.Objectives.Components;
using Content.Server._ClawCommand.Possession;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Devil.Condemned;
using Content.Shared._ClawCommand.Devil.Contract;
using Content.Shared._ClawCommand.Paper;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants;
using Content.Shared.Mindshield.Components;
using Content.Shared.Nutrition;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Silicon.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.Devil.Contract;

public sealed partial class DevilContractSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSubdermalImplantSystem _implant = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly SpriteSpecifier BurnIcon = new SpriteSpecifier.Rsi(new("/Textures/Effects/fire.rsi"), "fire");

    private ISawmill _sawmill = default!;

    private readonly Dictionary<LocId, Func<DevilContractComponent, EntityUid?>> _targetResolvers = new()
    {
        // The contractee is who is making the deal.
        ["devil-contract-contractee"] = comp => comp.Signer,
        // The contractor is the entity offering the deal.
        ["devil-contract-contractor"] = comp => comp.ContractOwner,
    };

    private Regex _clauseRegex = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeRegex();
        InitializeSpecialActions();

        SubscribeLocalEvent<DevilContractComponent, BeingSignedAttemptEvent>(OnContractSignAttempt);
        SubscribeLocalEvent<DevilContractComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DevilContractComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DevilContractComponent, SignSuccessfulEvent>(OnSignStep);
        SubscribeLocalEvent<DevilContractComponent, FullyEatenEvent>(OnEaten);

        _sawmill = Logger.GetSawmill("devil-contract");
    }

    private void InitializeRegex()
    {
        var targetPattern = string.Join("|", _targetResolvers.Keys.Select(locId => Regex.Escape(Loc.GetString(locId))));

        _clauseRegex = new Regex($@"^\s*(?<target>{targetPattern})\s*:\s*(?<clause>.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
    }

    private void OnGetVerbs(Entity<DevilContractComponent> contract, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract
            || !args.CanAccess
            || !TryComp<DevilComponent>(args.User, out var devilComp))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryBurnContract(contract, (user, devilComp)),
            Text = Loc.GetString("burn-contract-prompt"),
            Icon = BurnIcon,
        });
    }

    private void TryBurnContract(Entity<DevilContractComponent> contract, Entity<DevilComponent> devil)
    {
        var coordinates = Transform(contract).Coordinates;

        if (contract.Comp.IsContractFullySigned)
        {
            _popup.PopupCoordinates(Loc.GetString("burn-contract-popup-fail"), coordinates, devil, PopupType.MediumCaution);
            return;
        }

        Spawn(devil.Comp.FireEffectProto, coordinates);
        _audio.PlayPvs(devil.Comp.FwooshPath, coordinates, AudioParams.Default.WithVolume(-2f));
        QueueDel(contract);
    }

    private void OnExamined(Entity<DevilContractComponent> contract, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        UpdateContractWeight(contract);
        args.PushMarkup(Loc.GetString("devil-contract-examined", ("weight", contract.Comp.ContractWeight)));
    }

    private void OnEaten(Entity<DevilContractComponent> contract, ref FullyEatenEvent args)
    {
        // contract explosions should not cause any kind of major structural damage. you should at worst need to weld a window or repair a table.
        _explosion.QueueExplosion(
            args.User,
            typeId: "Default",
            totalIntensity: 1,
            slope: 1,
            maxTileIntensity: 1,
            maxTileBreak: 0,
            addLog: false);
    }

    #region Signing Steps

    private void OnContractSignAttempt(Entity<DevilContractComponent> contract, ref BeingSignedAttemptEvent args)
    {
        // Make sure that weight is set properly!
        UpdateContractWeight(contract);

        // Don't allow mortals to sign contracts for other people.
        if (contract.Comp.IsVictimSigned && args.Signer != contract.Comp.ContractOwner)
        {
            _popup.PopupEntity(Loc.GetString("devil-sign-invalid-user"), contract, args.Signer);
            args.Cancelled = true;
            return;
        }

        // Only handle unsigned contracts.
        if (contract.Comp.IsVictimSigned || contract.Comp.IsDevilSigned)
            return;

        // Check if devil is trying to sign first
        if (args.Signer == contract.Comp.ContractOwner)
        {
            _popup.PopupEntity(Loc.GetString("devil-contract-early-sign-failed"), args.Signer, args.Signer, PopupType.MediumCaution);
            args.Cancelled = true;
            return;
        }

        if (!IsUserValid(args.Signer, out var failReason))
        {
            _popup.PopupEntity(failReason, contract, args.Signer, PopupType.MediumCaution);
            args.Cancelled = true;
            return;
        }

        // Check if the weight is too low
        if (!contract.Comp.IsContractSignable)
        {
            var difference = Math.Abs(contract.Comp.ContractWeight);
            _popup.PopupEntity(Loc.GetString("contract-uneven-odds", ("number", difference)), contract, args.Signer, PopupType.MediumCaution);
            args.Cancelled = true;
        }
    }

    private void OnSignStep(Entity<DevilContractComponent> contract, ref SignSuccessfulEvent args)
    {
        // Determine signing phase
        if (!contract.Comp.IsVictimSigned)
            HandleVictimSign(contract, args.Paper, args.Signer);
        else if (!contract.Comp.IsDevilSigned)
            HandleDevilSign(contract, args.Paper, args.Signer);
        else
            return;

        // Final activation check
        if (contract.Comp.IsContractFullySigned)
            DoContractEffects(contract);
    }

    private void HandleVictimSign(Entity<DevilContractComponent> contract, EntityUid signed, EntityUid signer)
    {
        contract.Comp.Signer = signer;
        contract.Comp.IsVictimSigned = true;

        if (TryComp<PaperComponent>(contract, out var paper))
            paper.EditingDisabled = true;

        _popup.PopupEntity(Loc.GetString("contract-victim-signed"), signed, signer);

        var signedEv = new DevilContractSignedEvent(contract);
        RaiseLocalEvent(signer, ref signedEv);
    }

    private void HandleDevilSign(Entity<DevilContractComponent> contract, EntityUid signed, EntityUid signer)
    {
        contract.Comp.IsDevilSigned = true;
        _popup.PopupEntity(Loc.GetString("contract-devil-signed"), signed, signer);

        if (_mind.TryGetMind(signer, out var mindId, out var mind)
            && _mind.TryGetObjectiveComp<MeetContractWeightConditionComponent>(mindId, out var objectiveComp, mind))
            objectiveComp.ContractWeight += contract.Comp.ContractWeight;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Does this entity have a soul it is allowed to sell?
    /// </summary>
    public bool IsUserValid(EntityUid user, out string failReason)
    {
        if (HasComp<CondemnedComponent>(user)
            || HasComp<SiliconComponent>(user)
            || HasComp<BorgChassisComponent>(user))
        {
            failReason = Loc.GetString("devil-contract-no-soul-sign-failed");
            return false;
        }

        if (HasComp<MindShieldComponent>(user) && !HasComp<DevilComponent>(user))
        {
            failReason = Loc.GetString("devil-contract-mind-shielded-failed");
            return false;
        }

        if (HasComp<PossessedComponent>(user))
        {
            failReason = Loc.GetString("devil-contract-early-sign-failed");
            return false;
        }

        failReason = string.Empty;
        return true;
    }

    /// <summary>
    /// Hands the contractee's soul to the devil. They will be dragged to hell on their final death.
    /// </summary>
    public bool TryTransferSouls(EntityUid devil, EntityUid contractee, int added)
    {
        if (TerminatingOrDeleted(contractee))
            return false;

        // Can't sell what doesn't exist.
        if (HasComp<CondemnedComponent>(contractee) || devil == contractee)
            return false;

        var ev = new SoulAmountChangedEvent(devil, contractee, added);
        RaiseLocalEvent(devil, ref ev);

        var condemned = EnsureComp<CondemnedComponent>(contractee);
        condemned.SoulOwner = devil;
        condemned.CondemnOnDeath = true;

        return true;
    }

    private void UpdateContractWeight(Entity<DevilContractComponent> contract)
    {
        if (!TryComp<PaperComponent>(contract, out var paper))
            return;

        contract.Comp.CurrentClauses.Clear();
        var newWeight = 0;

        foreach (Match match in _clauseRegex.Matches(paper.Content))
        {
            if (!TryGetClause(match, out var clauseProto) || !contract.Comp.CurrentClauses.Add(clauseProto))
                continue;

            newWeight += clauseProto.ClauseWeight;
        }

        contract.Comp.ContractWeight = newWeight;
    }

    private bool TryGetClause(Match match, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DevilClausePrototype? clause)
    {
        clause = null;
        if (!match.Success)
            return false;

        var clauseKey = match.Groups["clause"].Value.Trim().ToLowerInvariant().Replace(" ", "");
        return _prototypeManager.TryIndex(clauseKey, out clause);
    }

    private void DoContractEffects(Entity<DevilContractComponent> contract)
    {
        if (!TryComp<PaperComponent>(contract, out var paper))
            return;

        UpdateContractWeight(contract);

        var processedClauses = new HashSet<string>();

        foreach (Match match in _clauseRegex.Matches(paper.Content))
        {
            if (!match.Success)
                continue;

            var targetKey = match.Groups["target"].Value.Trim();
            var resolver = _targetResolvers.FirstOrDefault(pair => Loc.GetString(pair.Key).Equals(targetKey, StringComparison.OrdinalIgnoreCase)).Value;

            if (resolver?.Invoke(contract.Comp) is not { } targetEntity)
            {
                _sawmill.Warning($"No target for \"{targetKey}\" on contract {ToPrettyString(contract)}");
                continue;
            }

            if (!TryGetClause(match, out var clause))
                continue;

            // no duplicates
            if (!processedClauses.Add(clause.ID))
                continue;

            ApplyEffectToTarget(targetEntity, clause, contract);
        }
    }

    private void ApplyEffectToTarget(EntityUid target, DevilClausePrototype clause, Entity<DevilContractComponent>? contract)
    {
        if (TerminatingOrDeleted(target))
            return;

        // Polymorphing swaps the entity, so everything else goes onto the new body.
        if (clause.Polymorph is { } polymorph && _polymorph.PolymorphEntity(target, polymorph) is { } newEntity)
            target = newEntity;

        if (clause.RemovedComponents != null)
            EntityManager.RemoveComponents(target, clause.RemovedComponents);

        // Overwrite, so clauses like "fearofdeath" can replace the target's existing thresholds.
        if (clause.AddedComponents != null)
            EntityManager.AddComponents(target, clause.AddedComponents, true);

        if (clause.StatusEffects != null)
        {
            foreach (var effect in clause.StatusEffects)
                _statusEffects.TrySetStatusEffectDuration(target, effect);
        }

        if (clause.DamageModifierSet != null)
            _damageable.SetDamageModifierSetId(target, clause.DamageModifierSet);

        if (clause.Implants != null)
            _implant.AddImplants(target, clause.Implants);

        if (clause.SpawnedItems != null)
        {
            foreach (var item in clause.SpawnedItems)
            {
                var spawnedItem = SpawnNextToOrDrop(item, target);
                _hands.TryPickupAnyHand(target, spawnedItem, false, false, false);
            }
        }

        if (clause.Event is { } ev)
        {
            ev.Target = target;
            ev.Contract = contract?.Comp;
            RaiseLocalEvent(target, (object) ev, true);
        }
    }

    public void AddRandomNegativeClause(EntityUid target)
    {
        AddRandomClause(target, c => c.ClauseWeight >= 0);
    }

    public void AddRandomPositiveClause(EntityUid target)
    {
        AddRandomClause(target, c => c.ClauseWeight <= 0);
    }

    public void AddRandomClause(EntityUid target)
    {
        AddRandomClause(target, _ => true);
    }

    private void AddRandomClause(EntityUid target, Func<DevilClausePrototype, bool> filter)
    {
        // The "chance" clause rolls a random clause itself, so it can't be rolled or it could chain forever.
        var clauses = _prototypeManager.EnumeratePrototypes<DevilClausePrototype>()
            .Where(c => c.Event is not DevilContractChanceEvent)
            .Where(filter)
            .ToList();

        if (clauses.Count == 0)
            return;

        var selectedClause = _random.Pick(clauses);
        ApplyEffectToTarget(target, selectedClause, null);

        _sawmill.Debug($"Selected {selectedClause.ID} effect for {ToPrettyString(target)}");
    }

    #endregion

    #region Special clause events

    private void InitializeSpecialActions()
    {
        SubscribeLocalEvent<DevilContractSoulOwnershipEvent>(OnSoulOwnership);
        SubscribeLocalEvent<DevilContractChanceEvent>(OnChance);
    }

    private void OnSoulOwnership(DevilContractSoulOwnershipEvent args)
    {
        if (args.Contract?.ContractOwner is not { } contractOwner)
            return;

        TryTransferSouls(contractOwner, args.Target, 1);
    }

    // LETS GO GAMBLING!!!!!
    private void OnChance(DevilContractChanceEvent args)
    {
        AddRandomClause(args.Target);
    }

    #endregion
}
