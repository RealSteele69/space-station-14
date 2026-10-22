// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Goobstation.Common.Atmos;
using Content.Goobstation.Shared.Religion;
using Content.Server._ClawCommand.Devil.Condemned;
using Content.Server._ClawCommand.Devil.Contract;
using Content.Server._ClawCommand.Devil.Objectives.Components;
using Content.Server._ClawCommand.Possession;
using Content.Server._ClawCommand.Shadekin;
using Content.Server.Antag.Components;
using Content.Server.Destructible;
using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Shared._ClawCommand.CheatDeath;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Devil.Condemned;
using Content.Shared._ClawCommand.Lavaland.Chasm;
using Content.Shared._ClawCommand.Morgue;
using Content.Shared._ClawCommand.Religion;
using Content.Shared._ClawCommand.Supermatter.Components;
using Content.Shared.Actions;
using Content.Shared.Administration.Systems;
using Content.Shared.Bible.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Silicon.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Content.Shared.Zombies;
using Robust.Server.Containers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Devil;

public sealed partial class DevilSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private PolymorphSystem _poly = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private DevilContractSystem _contract = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PossessionSystem _possession = default!;
    [Dependency] private CondemnedSystem _condemned = default!;
    [Dependency] private MobStateSystem _state = default!;
    [Dependency] private SharedJitteringSystem _jittering = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly Regex WhitespaceAndNonWordRegex = new(@"[\s\W]+", RegexOptions.Compiled);

    private static readonly string[] EyeCoveringSlots = ["eyes", "mask", "head"];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<DevilComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DevilComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<DevilComponent, SoulAmountChangedEvent>(OnSoulAmountChanged);
        SubscribeLocalEvent<DevilComponent, PowerLevelChangedEvent>(OnPowerLevelChanged);
        SubscribeLocalEvent<DevilComponent, ExorcismDoAfterEvent>(OnExorcismDoAfter);

        InitializeHandshakeSystem();
        SubscribeAbilities();
    }

    #region Startup

    private void OnStartup(Entity<DevilComponent> devil, ref MapInitEvent args)
    {
        // Remove human components. Without combat mode the devil can't fight: deals, not violence.
        RemComp<CombatModeComponent>(devil);
        RemComp<SatiationComponent>(devil);
        RemComp<TemperatureComponent>(devil);
        RemComp<TemperatureSpeedComponent>(devil);
        RemComp<CondemnedComponent>(devil);
        RemComp<DestructibleComponent>(devil);

        // Adjust stats
        EnsureComp<ZombieImmuneComponent>(devil);
        EnsureComp<RespiratorImmuneComponent>(devil);
        EnsureComp<SpecialPressureImmunityComponent>(devil);
        EnsureComp<ActiveListenerComponent>(devil);
        EnsureComp<WeakToHolyComponent>(devil).AlwaysTakeHoly = true;
        EnsureComp<CrematoriumImmuneComponent>(devil);
        EnsureComp<AntagImmuneComponent>(devil);
        EnsureComp<SupermatterImmuneComponent>(devil);
        EnsureComp<PreventChasmFallingComponent>(devil).DeleteOnUse = false;
        EnsureComp<FTLSmashImmuneComponent>(devil);

        // Allow infinite revival
        var revival = EnsureComp<CheatDeathComponent>(devil);
        revival.InfiniteRevives = true;
        revival.CanCheatStanding = true;

        // Change damage modifier
        if (TryComp<DamageableComponent>(devil, out var damageable))
            _damageable.SetDamageModifierSetId((devil, damageable), devil.Comp.DevilDamageModifierSet);

        // Add base actions
        foreach (var actionId in devil.Comp.BaseDevilActions)
            _actions.AddAction(devil, actionId);

        GenerateTrueName(devil.Comp);
    }

    #endregion

    #region Event Listeners

    private void OnSoulAmountChanged(Entity<DevilComponent> devil, ref SoulAmountChangedEvent args)
    {
        devil.Comp.Souls += args.Amount;
        _popup.PopupEntity(Loc.GetString("contract-soul-added"), args.User, args.User, PopupType.MediumCaution);

        // Every second soul (2, 4, 6) unlocks the next power level.
        if (devil.Comp.Souls is > 1 and < 7 && devil.Comp.Souls % 2 == 0)
        {
            var newLevel = (DevilPowerLevel) (devil.Comp.Souls / 2);
            if (newLevel > devil.Comp.PowerLevel)
            {
                devil.Comp.PowerLevel = newLevel;

                var ev = new PowerLevelChangedEvent(args.User, devil.Comp.PowerLevel);
                RaiseLocalEvent(args.User, ref ev);
            }
        }

        if (_mind.TryGetMind(args.User, out var mindId, out var mind)
            && _mind.TryGetObjectiveComp<SignContractConditionComponent>(mindId, out var objectiveComp, mind))
            objectiveComp.ContractsSigned += args.Amount;
    }

    private void OnPowerLevelChanged(Entity<DevilComponent> devil, ref PowerLevelChangedEvent args)
    {
        var popup = Loc.GetString($"devil-power-level-increase-{args.NewLevel.ToString().ToLowerInvariant()}");
        _popup.PopupEntity(popup, args.User, args.User, PopupType.Large);

        if (!_prototype.TryIndex(devil.Comp.DevilBranchPrototype, out var proto)
            || !proto.PowerActions.TryGetValue(args.NewLevel, out var actions))
            return;

        foreach (var actionId in actions)
            _actions.AddAction(devil, actionId);
    }

    private void OnExamined(Entity<DevilComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange
            || ent.Comp.PowerLevel < DevilPowerLevel.Weak
            || IsEyesCovered(ent))
            return;

        args.PushMarkup(Loc.GetString("devil-component-examined", ("target", Identity.Entity(ent, EntityManager))));
    }

    /// <summary>
    /// Are this entity's eyes hidden by identity-blocking eyewear, a mask or a helmet?
    /// </summary>
    public bool IsEyesCovered(EntityUid uid)
    {
        foreach (var slot in EyeCoveringSlots)
        {
            if (_inventory.TryGetSlotEntity(uid, slot, out var item)
                && TryComp<IdentityBlockerComponent>(item, out var blocker)
                && blocker.Enabled)
                return true;
        }

        return false;
    }

    private void OnListen(Entity<DevilComponent> devil, ref ListenEvent args)
    {
        // Other Devils and entities without souls have no authority over you.
        if (HasComp<DevilComponent>(args.Source)
            || HasComp<CondemnedComponent>(args.Source)
            || HasComp<SiliconComponent>(args.Source)
            || args.Source == devil.Owner)
            return;

        var message = WhitespaceAndNonWordRegex.Replace(args.Message.ToLowerInvariant(), "");
        var trueName = WhitespaceAndNonWordRegex.Replace(devil.Comp.TrueName.ToLowerInvariant(), "");

        if (string.IsNullOrEmpty(trueName) || !message.Contains(trueName))
            return;

        // hardcoded, but this is just flavor
        _jittering.DoJitter(devil, TimeSpan.FromSeconds(4), true);

        if (_timing.CurTime < devil.Comp.LastTriggeredTime + devil.Comp.CooldownDuration)
            return;

        devil.Comp.LastTriggeredTime = _timing.CurTime;

        if (HasComp<BibleUserComponent>(args.Source))
        {
            _damageable.TryChangeDamage(devil.Owner, devil.Comp.DamageOnTrueName * devil.Comp.BibleUserDamageMultiplier, true);
            _stun.TryUpdateParalyzeDuration(devil, devil.Comp.ParalyzeDurationOnTrueName * devil.Comp.BibleUserDamageMultiplier);

            var popup = Loc.GetString("devil-true-name-heard-chaplain", ("speaker", args.Source), ("target", devil));
            _popup.PopupEntity(popup, devil, PopupType.LargeCaution);
        }
        else
        {
            _stun.TryUpdateParalyzeDuration(devil, devil.Comp.ParalyzeDurationOnTrueName);
            _damageable.TryChangeDamage(devil.Owner, devil.Comp.DamageOnTrueName, true);

            var popup = Loc.GetString("devil-true-name-heard", ("speaker", args.Source), ("target", devil));
            _popup.PopupEntity(popup, devil, PopupType.LargeCaution);
        }
    }

    private void OnExorcismDoAfter(Entity<DevilComponent> devil, ref ExorcismDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("devil-exorcised", ("target", Name(devil))), devil, PopupType.LargeCaution);
        _condemned.StartCondemnation(target, behavior: CondemnedBehavior.Banish, doFlavor: false);
    }

    #endregion

    #region Helper Methods

    private static bool TryUseAbility(BaseActionEvent action)
    {
        if (action.Handled)
            return false;

        action.Handled = true;
        return true;
    }

    private void PlayFwooshSound(EntityUid uid, DevilComponent comp)
    {
        _audio.PlayPvs(comp.FwooshPath, uid, AudioParams.Default.WithVolume(-2f));
    }

    private void DoContractFlavor(EntityUid devil, string name)
    {
        _popup.PopupEntity(Loc.GetString("contract-summon-flavor", ("name", name)), devil, PopupType.Medium);
    }

    private void GenerateTrueName(DevilComponent comp)
    {
        var firstNameOptions = _prototype.Index(comp.FirstNameTrue);
        var lastNameOptions = _prototype.Index(comp.LastNameTrue);

        comp.TrueName = string.Concat(_random.Pick(firstNameOptions.Values), " ", _random.Pick(lastNameOptions.Values));
    }

    #endregion
}
