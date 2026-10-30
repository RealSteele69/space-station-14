// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note (adaptations): DevilContractSystem.AddRandomNegativeClauseSlasher -> the generic
// AddRandomNegativeClause(target) (this fork's Devil contract system has no Slasher-specific
// overload). SharedWeatherSystem.SetWeather(mapId, WeatherPrototype, null) -> TrySetWeather(mapId,
// EntProtoId?, out EntityUid?, duration) (different signature on this fork). AlertLevelSystem moved
// from Content.Server.AlertLevel to Content.Shared.AlertLevel on this fork. Dropped the
// GhostBooEvent fallback in the light-flicker loop - that event doesn't exist here, so flickering
// just always attempts to pop the bulb (gated by the same probability roll source used).

using System.Linq;
using Content.Server._ClawCommand.Devil.Contract;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Light.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._GoobStation.Slasher;
using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared._GoobStation.Slasher.Objectives;
using Content.Shared._GoobStation.Slasher.Systems;
using Content.Shared.Actions;
using Content.Shared.AlertLevel;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Standing;
using Content.Shared.Station;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Station.Systems;

namespace Content.Server._GoobStation.Slasher.Systems;

/// <summary>
/// Soul steal system for the slasher. Gives bonuses for stealing souls from incapacitated or dead targets.
/// </summary>
public sealed partial class SlasherSoulStealSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DevilContractSystem _devilContractSystem = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private ServerStationSystem _stationSystem = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PoweredLightSystem _light = default!;
    [Dependency] private SlasherRegenerateSystem _regenerate = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherSoulStealComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlasherSoulStealComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SlasherSoulStealComponent, SlasherSoulStealEvent>(OnSoulSteal);
        SubscribeLocalEvent<SlasherSoulStealComponent, SlasherSoulStealDoAfterEvent>(OnSoulStealDoAfterComplete);
        SubscribeLocalEvent<SlasherSoulStealMacheteBonusComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<SlasherSoulStealMacheteBonusComponent, ThrowDoHitEvent>(OnThrowHit);
        SubscribeLocalEvent<SlasherSoulStealComponent, SlasherSummonMacheteEvent>(OnSummonMachete);
        SubscribeLocalEvent<SlasherSoulStealComponent, DidEquipHandEvent>(OnDidEquipHand);
        SubscribeLocalEvent<SlasherSoulStealComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnMapInit(Entity<SlasherSoulStealComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnShutdown(Entity<SlasherSoulStealComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.ActionEntity);
    }

    private void OnSoulSteal(Entity<SlasherSoulStealComponent> ent, ref SlasherSoulStealEvent args)
    {
        if (args.Handled || !args.Target.Valid)
            return;

        var user = ent.Owner;
        var target = args.Target;

        if (!_mindSystem.TryGetMind(target, out _, out MindComponent? _))
        {
            _popup.PopupEntity(Loc.GetString("slasher-soulsteal-fail-no-mind"), user, user);
            args.Handled = true;
            return;
        }

        if (HasComp<SoullessComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("slasher-soulsteal-fail-no-mind"), user, user);
            args.Handled = true;
            return;
        }

        if (!HasComp<MobStateComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("slasher-soulsteal-fail-not-valid"), user, user);
            args.Handled = true;
            return;
        }

        if (!_mobState.IsCritical(target)
            && !_mobState.IsIncapacitated(target)
            && !_standing.IsDown(target)
            && !_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("slasher-soulsteal-fail-not-down"), user, user);
            args.Handled = true;
            return;
        }

        // Starting the do-after on the next tick avoids modifying ActiveDoAfterComponent while active.
        Timer.Spawn(_timing.TickPeriod, () =>
        {
            if (!Exists(user) || !Exists(target))
                return;

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.SoulStealDoAfterDuration,
                new SlasherSoulStealDoAfterEvent(), user, target: target)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                DistanceThreshold = 2f,
                RequireCanInteract = false,
            });
        });

        _popup.PopupEntity(Loc.GetString("slasher-soulsteal-start", ("target", target)), user, user);
        _popup.PopupEntity(Loc.GetString("slasher-soulsteal-start-victim", ("user", user)), target, target, PopupType.MediumCaution);
        args.Handled = true;
    }

    private void OnSoulStealDoAfterComplete(Entity<SlasherSoulStealComponent> ent, ref SlasherSoulStealDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Args.Target == null)
            return;

        var user = ent.Owner;
        var target = ev.Args.Target.Value;
        var comp = ent.Comp;

        _audio.PlayPvs(ent.Comp.SoulStealSound, target);

        var tileMix = _atmos.GetTileMixture(target, excite: true);
        tileMix?.AdjustMoles(Gas.Ammonia, comp.MolesAmmonia);

        var alive = _mobState.IsAlive(target);

        var bruteBonus = alive ? comp.AliveBruteBonusPerSoul : comp.DeadBruteBonusPerSoul;
        var armorBonus = alive ? comp.AliveArmorPercentPerSoul : comp.DeadArmorPercentPerSoul;

        if (alive)
            comp.AliveSouls++;
        else
            comp.DeadSouls++;

        if (_mindSystem.TryGetMind(user, out _, out var mind))
        {
            foreach (var objUid in mind.Objectives.ToList())
            {
                if (!TryComp<SlasherAbsorbSoulsConditionComponent>(objUid, out var absorbObj))
                    continue;

                absorbObj.Absorbed += 1;
                Dirty(objUid, absorbObj);
                break;
            }
        }

        _devilContractSystem.AddRandomNegativeClause(target);

        EnsureComp<SoullessComponent>(target);

        ApplyArmorBonus(armorBonus, comp);
        ApplyMacheteBonus(user, bruteBonus, comp);

        var totalSouls = comp.AliveSouls + comp.DeadSouls;
        var specialUnlockHappened = false;

        if (!comp.HasUnlockedPossession && totalSouls >= comp.PossessionSoulThreshold)
        {
            comp.HasUnlockedPossession = true;
            EnsureComp<SlasherPossessionComponent>(user);

            _popup.PopupEntity(Loc.GetString("slasher-soulsteal-unlock-possession"), user, user, PopupType.LargeCaution);
            specialUnlockHappened = true;
        }

        if (!comp.HasAscended && totalSouls >= comp.AscendanceSoulThreshold)
        {
            comp.HasAscended = true;

            RaiseLocalEvent(new SlasherAscendedEvent(user));

            comp.NextLightFlicker = _timing.CurTime + comp.LightFlickerInterval;

            var station = _stationSystem.GetOwningStation(user);
            if (station != null)
            {
                _alertLevel.SetLevel(station.Value, "red", true, true, true, false);

                if (comp.AscensionGear != null)
                    ApplyAscensionGear(user, comp.AscensionGear.Value);

                _chatSystem.DispatchStationAnnouncement(
                    station.Value,
                    Loc.GetString(comp.AscendanceAnnouncementKey),
                    sender: Loc.GetString("comms-console-announcement-title-centcom"),
                    playDefaultSound: false,
                    announcementSound: null,
                    colorOverride: Color.Red);

                _audio.PlayGlobal(comp.AscendanceSound, Filter.Broadcast(), true);
            }
        }

        _regenerate.GrantSoul(user);

        if (!specialUnlockHappened)
            _popup.PopupEntity(Loc.GetString("slasher-soulsteal-success", ("target", target)), user, user, PopupType.LargeCaution);

        _popup.PopupEntity(Loc.GetString("slasher-soulsteal-success-victim", ("user", user)), target, target, PopupType.LargeCaution);
        Dirty(user, comp);
    }

    /// <summary>
    /// Strips the slots covered by the ascension gear, then equips it.
    /// </summary>
    private void ApplyAscensionGear(EntityUid user, ProtoId<StartingGearPrototype> gearProto)
    {
        if (!_protoMan.TryIndex(gearProto, out var loadout))
            return;

        if (_inventory.TryGetSlots(user, out var slots))
        {
            foreach (var slot in slots)
            {
                if (string.IsNullOrEmpty(((IEquipmentLoadout) loadout).GetGear(slot.Name)))
                    continue;

                if (_inventory.TryGetSlotEntity(user, slot.Name, out var worn) && worn != null)
                {
                    _inventory.TryUnequip(user, slot.Name, silent: true, force: true);
                    QueueDel(worn.Value);
                }
            }
        }

        _spawning.EquipStartingGear(user, loadout);
    }

    private static void ApplyArmorBonus(float percent, SlasherSoulStealComponent comp)
    {
        if (percent <= 0f)
            return;

        comp.ArmorReduction = MathF.Min(comp.ArmorReduction + percent, comp.ArmorCap);
    }

    private void OnDamageModify(Entity<SlasherSoulStealComponent> ent, ref DamageModifyEvent args)
    {
        var reduction = ent.Comp.ArmorReduction;
        if (reduction <= 0f || args.Damage.Empty)
            return;

        var pairs = args.Damage.DamageDict.ToArray();
        var factor = 1f - reduction;
        foreach (var (type, val) in pairs)
        {
            if (val <= FixedPoint2.Zero)
                continue; // don't scale healing
            args.Damage.DamageDict[type] = val * factor;
        }
    }

    private EntityUid? GetMachete(EntityUid user)
    {
        if (TryComp<SlasherSummonMacheteComponent>(user, out var summon) && summon.MacheteUid != null && Exists(summon.MacheteUid.Value))
            return summon.MacheteUid.Value;

        if (!TryComp<HandsComponent>(user, out var hands))
            return null;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
            if (HasComp<SlasherMassacreMacheteComponent>(held))
                return held;

        return null;
    }

    private void ApplyMacheteBonus(EntityUid user, float bruteBonus, SlasherSoulStealComponent comp)
    {
        if (bruteBonus <= 0f)
            return;

        var machete = GetMachete(user);
        if (machete == null)
            return;

        var bonusComp = EnsureComp<SlasherSoulStealMacheteBonusComponent>(machete.Value);
        bonusComp.SlashBonus += bruteBonus;
        comp.TotalAppliedBruteBonus += bruteBonus;
        comp.LastMachete = machete.Value;
        Dirty(machete.Value, bonusComp);
    }

    private void OnGetMeleeDamage(Entity<SlasherSoulStealMacheteBonusComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.SlashBonus <= 0f)
            return;

        var add = new DamageSpecifier();
        add.DamageDict.Add("Slash", ent.Comp.SlashBonus);
        args.Damage += add;
    }

    private void OnThrowHit(Entity<SlasherSoulStealMacheteBonusComponent> ent, ref ThrowDoHitEvent args)
    {
        if (ent.Comp.SlashBonus <= 0f || TerminatingOrDeleted(args.Target))
            return;

        var dmgAdj = new DamageSpecifier();
        dmgAdj.DamageDict.Add("Slash", ent.Comp.SlashBonus);
        _damageable.TryChangeDamage(args.Target, dmgAdj, true, origin: args.Component.Thrower);
    }

    private void OnSummonMachete(Entity<SlasherSoulStealComponent> ent, ref SlasherSummonMacheteEvent args)
    {
        var machete = GetMachete(ent.Owner);
        if (machete == null)
            return;

        ent.Comp.LastMachete = machete.Value;

        if (ent.Comp.TotalAppliedBruteBonus > 0f)
        {
            var bonusComp = EnsureComp<SlasherSoulStealMacheteBonusComponent>(machete.Value);
            bonusComp.SlashBonus = ent.Comp.TotalAppliedBruteBonus;
            Dirty(machete.Value, bonusComp);
        }

        Dirty(ent);
    }

    private void OnDidEquipHand(Entity<SlasherSoulStealComponent> ent, ref DidEquipHandEvent args)
    {
        if (!HasComp<SlasherMassacreMacheteComponent>(args.Equipped))
            return;

        ent.Comp.LastMachete = args.Equipped;

        if (ent.Comp.TotalAppliedBruteBonus > 0f)
        {
            var bonusComp = EnsureComp<SlasherSoulStealMacheteBonusComponent>(args.Equipped);
            bonusComp.SlashBonus = ent.Comp.TotalAppliedBruteBonus;
            Dirty(args.Equipped, bonusComp);
        }

        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SlasherSoulStealComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.HasAscended || _timing.CurTime < comp.NextLightFlicker)
                continue;

            FlickerLightsAround(uid, comp);
            comp.NextLightFlicker = _timing.CurTime + comp.LightFlickerInterval;
            Dirty(uid, comp);
        }
    }

    private void FlickerLightsAround(EntityUid slasher, SlasherSoulStealComponent comp)
    {
        var entities = _lookup.GetEntitiesInRange(slasher, comp.LightFlickerRadius).ToList();
        _random.Shuffle(entities);

        var flickerCounter = 0;
        foreach (var entity in entities)
        {
            if (!HasComp<PointLightComponent>(entity) || !TryComp<PoweredLightComponent>(entity, out var lightComp))
                continue;

            if (_random.Prob(0.85f) && _light.TryDestroyBulb(entity, lightComp))
                flickerCounter++;

            if (flickerCounter >= comp.MaxLightsToFlicker)
                break;
        }
    }
}
