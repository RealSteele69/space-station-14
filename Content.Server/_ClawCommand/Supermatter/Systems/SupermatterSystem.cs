// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Lightning;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._ClawCommand.Emitters;
using Content.Shared._ClawCommand.Supermatter.Components;
using Content.Shared._ClawCommand.Supermatter.Events;
using Content.Shared._ClawCommand.Supermatter.Monitor;
using Content.Shared._ClawCommand.Supermatter.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.AlertLevel;
using Content.Shared.Atmos;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Radiation.Components;
using Content.Shared.Radiation.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Supermatter.Systems;

public sealed partial class SupermatterSystem : SharedSupermatterSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private AmbientSoundSystem _ambient = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RadioSystem _radioSystem = default!;
    [Dependency] private IChatManager _chatmanager = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRadiationSystem _radiation = default!;

    private static readonly ProtoId<ToolQualityPrototype> SlicingQuality = "Slicing";
    private static readonly ProtoId<AlertLevelPrototype> DelamAlertLevel = "Yellow";
    private static readonly ProtoId<AlertLevelPrototype> SevereDelamAlertLevel = "DeltaSupermatter";
    private static readonly EntProtoId AshPrototype = "Ash";

    private DelamType _delamType = DelamType.Explosion;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SupermatterComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<SupermatterComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<SupermatterComponent, StartCollideEvent>(OnCollideEvent);
        SubscribeLocalEvent<SupermatterComponent, InteractHandEvent>(OnHandInteract);
        SubscribeLocalEvent<SupermatterComponent, InteractUsingEvent>(OnItemInteract);
        SubscribeLocalEvent<SupermatterComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<SupermatterComponent, SupermatterDoAfterEvent>(OnGetSliver);
    }

    private void OnComponentRemove(EntityUid uid, SupermatterComponent component, ComponentRemove args)
    {
        // turn off any ambient if component is removed (ex. entity deleted)
        _ambient.SetAmbience(uid, false);
        component.AudioStream = _audio.Stop(component.AudioStream);
    }

    private void OnMapInit(EntityUid uid, SupermatterComponent component, MapInitEvent args)
    {
        // Set the Sound
        _ambient.SetAmbience(uid, true);

        //Add Air to the initialized SM in the Map so it doesnt delam on default
        var mix = _atmosphere.GetContainingMixture(uid, true, true);
        mix?.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
        mix?.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SupermatterComponent>();
        while (query.MoveNext(out var uid, out var sm))
        {
            if (!sm.Activated)
                continue;

            sm.UpdateAccumulator += frameTime;

            if (sm.UpdateAccumulator >= sm.UpdateTimer)
            {
                sm.UpdateAccumulator -= sm.UpdateTimer;
                Cycle(uid, sm);
            }
        }
    }

    public void Cycle(EntityUid uid, SupermatterComponent sm)
    {
        if (sm.TimeLocked < _gameTiming.CurTime.TotalMinutes - sm.TimeToUnlock && sm.Surge)
        {
            sm.Surge = false;
            _chatmanager.SendAdminAlert($"SM variables unlocked at time {_gameTiming.CurTime.TotalMinutes}");
        }
        sm.ZapAccumulator++;
        sm.YellAccumulator++;

        ProcessAtmos(uid, sm);
        HandleDamage(uid, sm);

        if (sm.Surge == false)
            AdjustSetpoints(sm);

        if (sm.Damage >= sm.DelaminationPoint || sm.Delamming)
            HandleDelamination(uid, sm);

        HandleSoundLoop(uid, sm);
        UpdateStatus(sm);

        if (sm.ZapAccumulator >= sm.ZapTimer)
        {
            sm.ZapAccumulator -= sm.ZapTimer;
            SupermatterZap(uid, sm);
        }

        if (sm.YellAccumulator >= sm.YellTimer)
        {
            sm.YellAccumulator -= sm.YellTimer;
            HandleAnnouncements(uid, sm);
        }

        if (sm.SMAngerValue < 0f)
        {
            sm.SMAngerValue = 0f; //no negative numbers plz
        }
        else if (sm.SMAngerValue >= sm.SMEventSetpoint) //if we are above the setpoint, do something
        {
            sm.SMAngerValue = 0f;
            RunEvent(uid, sm, GetEventType(sm));
        }
    }

    #region Processing

    /// <summary>
    ///     Handle power and radiation output depending on atmospheric things.
    /// </summary>
    private void ProcessAtmos(EntityUid uid, SupermatterComponent sm)
    {
        var mix = _atmosphere.GetContainingMixture(uid, true, true);

        if (mix is not { })
            return;

        var absorbedGas = mix.Remove(sm.GasEfficiency * mix.TotalMoles);
        var moles = absorbedGas.TotalMoles;

        if (!(moles > 0f))
            return;

        var gases = sm.GasStorage;
        var facts = sm.GasDataFields;

        //Lets get the proportions of the gasses in the mix for scaling stuff later
        //They range between 0 and 1
        gases = gases.ToDictionary(
            gas => gas.Key,
            gas => Math.Clamp(absorbedGas.GetMoles(gas.Key) / moles, 0, 1)
        );

        //No less then zero, and no greater then one, we use this to do explosions and heat to power transfer.
        var powerRatio = gases.Sum(gas => gases[gas.Key] * facts[gas.Key].PowerMixRatio);

        // Minimum value of -10, maximum value of 23. Affects plasma, o2 and heat output.
        var heatModifier = gases.Sum(gas => gases[gas.Key] * facts[gas.Key].HeatPenalty);

        // Minimum value of -10, maximum value of 23. Affects plasma, o2 and heat output.
        var transmissionBonus = gases.Sum(gas => gases[gas.Key] * facts[gas.Key].TransmitModifier);

        var h2OBonus = 1 - gases[Gas.WaterVapor] * 0.25f;

        var angerModifier = gases.Sum(gas => gases[gas.Key] * facts[gas.Key].AngerValue);

        powerRatio = Math.Clamp(powerRatio, 0, 1);
        heatModifier = Math.Max(heatModifier, 0.5f);
        transmissionBonus *= h2OBonus;

        sm.SMAngerValue += angerModifier;
        sm.SMLastAnger = angerModifier;

        // Effects the damage heat does to the crystal
        sm.DynamicHeatResistance = 1f;

        // more moles of gases are harder to heat than fewer,
        // so let's scale heat damage around them
        sm.MoleHeatPenaltyThreshold = (float) Math.Max(moles * sm.MoleHeatPenalty, 0.25);

        // Ramps up or down in increments of 0.02 up to the proportion of co2
        // Given infinite time, powerloss_dynamic_scaling = co2comp
        // Some value between 0 and 1
        if (moles > sm.PowerlossInhibitionMoleThreshold && gases[Gas.CarbonDioxide] > sm.PowerlossInhibitionGasThreshold)
        {
            var co2powerloss = Math.Clamp(gases[Gas.CarbonDioxide] - sm.PowerlossDynamicScaling, -0.02f, 0.02f);
            sm.PowerlossDynamicScaling = Math.Clamp(sm.PowerlossDynamicScaling + co2powerloss, 0f, 1f);
        }
        else
        {
            sm.PowerlossDynamicScaling = Math.Clamp(sm.PowerlossDynamicScaling - 0.05f, 0f, 1f);
        }

        // Ranges from 0 to 1(1-(value between 0 and 1 * ranges from 1 to 1.5(mol / 500)))
        // We take the mol count, and scale it to be our inhibitor
        var powerlossInhibitor =
            Math.Clamp(
                1 - sm.PowerlossDynamicScaling *
                Math.Clamp(moles / sm.PowerlossInhibitionMoleBoostThreshold, 1f, 1.5f),
                0f, 1f);

        if (sm.MatterPower != 0) //We base our removed power off one 10th of the matter_power.
        {
            var removedMatter = Math.Max(sm.MatterPower / sm.MatterPowerConversion, 40);
            //Adds at least 40 power
            sm.Power = Math.Max(sm.Power + removedMatter, 0);
            //Removes at least 40 matter power
            sm.MatterPower = Math.Max(sm.MatterPower - removedMatter, 0);
        }

        //based on gas mix, makes the power more based on heat or less effected by heat
        var tempFactor = powerRatio > 0.8 ? 50f : 30f;

        //if there is more n2 then anything else, we receive no power increase from heat
        sm.Power = Math.Max(absorbedGas.Temperature * tempFactor / Atmospherics.T0C * powerRatio + sm.Power, 0);

        //Radiate stuff
        if (TryComp<RadiationSourceComponent>(uid, out var rad))
        {
            var transmittedpower = sm.Power * Math.Max(0, 1f + transmissionBonus / 10f);
            _radiation.SetIntensity((uid, rad), transmittedpower * sm.RadiationOutputFactor);
        }

        //Power * 0.55 * a value between 1 and 0.8
        var energy = sm.Power * sm.ReactionPowerModifier;

        // Keep in mind we are only adding this temperature to (efficiency)% of the one tile the rock
        // is on. An increase of 4*C @ 25% efficiency here results in an increase of 1*C / (#tilesincore) overall.
        // Power * 0.55 * (some value between 1.5 and 23) / 5
        absorbedGas.Temperature += energy * heatModifier * sm.ThermalReleaseModifier;
        absorbedGas.Temperature = Math.Max(0,
            Math.Min(absorbedGas.Temperature, sm.HeatThreshold * heatModifier));

        // Release the waste
        absorbedGas.AdjustMoles(Gas.Plasma, Math.Max(energy * heatModifier * sm.PlasmaReleaseModifier, 0f));
        absorbedGas.AdjustMoles(Gas.Oxygen, Math.Max((energy + absorbedGas.Temperature * heatModifier - Atmospherics.T0C) * sm.OxygenReleaseEfficiencyModifier, 0f));

        _atmosphere.Merge(mix, absorbedGas);

        var powerReduction = (float) Math.Pow(sm.Power / 500, 3);

        // After this point power is lowered
        // This wraps around to the begining of the function
        sm.Power = Math.Max(sm.Power - Math.Min(powerReduction * powerlossInhibitor, sm.Power * 0.83f * powerlossInhibitor), 0f);

        sm.GasStorage = sm.GasStorage.ToDictionary(
            gas => gas.Key,
            gas => absorbedGas.GetMoles(gas.Key)
        );

        // Shown on the monitoring console
        sm.Temperature = absorbedGas.Temperature;
        sm.WasteMultiplier = heatModifier;
    }

    /// <summary>
    ///     Shoot lightning bolts depensing on accumulated power.
    /// </summary>
    private void SupermatterZap(EntityUid uid, SupermatterComponent sm)
    {
        // Divide power by it's threshold to get a value from 0 to 1, then multiply by the amount of possible lightnings
        // Makes it pretty obvious that if SM is shooting out red lightnings something is wrong.
        // And if it shoots too weak lightnings it means that it's underfed. Feed the SM :godo:
        var zapPower = sm.Power / sm.PowerPenaltyThreshold * sm.LightningPrototypes.Length;
        var zapPowerNorm = (int) Math.Clamp(zapPower, 0, sm.LightningPrototypes.Length - 1);
        _lightning.ShootRandomLightnings(uid, 3.5f, sm.Power > sm.PowerPenaltyThreshold ? 3 : 1, sm.LightningPrototypes[zapPowerNorm]);
    }

    /// <summary>
    ///     Handles environmental damage.
    /// </summary>
    private void HandleDamage(EntityUid uid, SupermatterComponent sm)
    {
        var xform = Transform(uid);
        var indices = _xform.GetGridOrMapTilePosition(uid, xform);

        sm.DamageArchived = sm.Damage;

        var mix = _atmosphere.GetContainingMixture(uid, true, true);

        // We're in space or there is no gas to process
        if (!xform.GridUid.HasValue || mix is not { } || mix.TotalMoles == 0f)
        {
            sm.Damage += Math.Max(sm.Power / 1000 * sm.DamageIncreaseMultiplier, 0.1f);
            return;
        }

        // Absorbed gas from surrounding area
        var absorbedGas = mix.Remove(sm.GasEfficiency * mix.TotalMoles);
        var moles = absorbedGas.TotalMoles;

        var totalDamage = 0f;

        var tempThreshold = Atmospherics.T0C + sm.HeatPenaltyThreshold;

        // Temperature start to have a positive effect on damage after 350
        var tempDamage = Math.Max(Math.Clamp(moles / 200f, .5f, 1f) * absorbedGas.Temperature - tempThreshold * sm.DynamicHeatResistance, 0f) * sm.MoleHeatThreshold / 150f * sm.DamageIncreaseMultiplier;
        totalDamage += tempDamage;

        // Power only starts affecting damage when it is above 5000
        var powerDamage = Math.Max(sm.Power - sm.PowerPenaltyThreshold, 0f) / 500f * sm.DamageIncreaseMultiplier;
        totalDamage += powerDamage;

        // Molar count only starts affecting damage when it is above 1800
        var moleDamage = Math.Max(moles - sm.MolePenaltyThreshold, 0) / 80 * sm.DamageIncreaseMultiplier;
        totalDamage += moleDamage;

        // Healing damage
        if (moles < sm.MolePenaltyThreshold)
        {
            // left there a very small float value so that it doesn't eventually divide by 0.
            var healHeatDamage = Math.Min(absorbedGas.Temperature - tempThreshold, 0.001f) / 150;
            totalDamage += healHeatDamage;
        }

        // Check for space tiles next to SM
        // TODO: change moles out for checking if adjacent tiles exist
        var enumerator = _atmosphere.GetAdjacentTileMixtures(xform.GridUid.Value, indices, false, false);
        while (enumerator.MoveNext(out var ind))
        {
            if (ind.TotalMoles != 0)
                continue;

            var integrity = GetIntegrity(sm);

            // this is some magic number shit
            var factor = integrity switch
            {
                < 10 => 0.0005f,
                < 25 => 0.0009f,
                < 45 => 0.005f,
                < 75 => 0.002f,
                _ => 0f,
            };

            totalDamage += Math.Clamp(sm.Power * factor * sm.DamageIncreaseMultiplier, 0, sm.MaxSpaceExposureDamage);

            break;
        }

        sm.Damage = Math.Min(sm.DamageArchived + sm.DamageHardcap * sm.DelaminationPoint, totalDamage);

        // Return the manipulated gas back to the mix
        _atmosphere.Merge(mix, absorbedGas);
    }

    /// <summary>
    ///     Handles announcements.
    /// </summary>
    private void HandleAnnouncements(EntityUid uid, SupermatterComponent sm)
    {
        var message = string.Empty;
        var global = false;

        var integrity = GetIntegrity(sm).ToString("0.00");

        // Special cases
        if (sm.Damage < sm.DelaminationPoint && sm.Delamming)
        {
            sm.DelamAnnounced = false;
            SupermatterAnnouncement(uid, Loc.GetString("supermatter-delam-cancel", ("integrity", integrity)), true);
            return;
        }
        if (sm.Delamming && !sm.DelamAnnounced)
        {
            var sb = new StringBuilder();
            string loc;
            var alertLevel = DelamAlertLevel;

            switch (_delamType)
            {
                case DelamType.Explosion:
                default:
                    loc = "supermatter-delam-explosion";
                    break;

                case DelamType.Singulo:
                    loc = "supermatter-delam-overmass";
                    alertLevel = SevereDelamAlertLevel;
                    break;

                case DelamType.Tesla:
                    loc = "supermatter-delam-tesla";
                    alertLevel = SevereDelamAlertLevel;
                    break;

                case DelamType.Cascade:
                    loc = "supermatter-delam-cascade";
                    alertLevel = SevereDelamAlertLevel;
                    break;
            }

            var station = _station.GetOwningStation(uid);
            if (station != null)
                _alert.SetLevel(station.Value, alertLevel, true, true, true, false);

            sb.AppendLine(Loc.GetString(loc));
            sb.AppendLine(Loc.GetString("supermatter-seconds-before-delam", ("seconds", sm.DelamTimer)));

            message = sb.ToString();
            global = true;
            sm.DelamAnnounced = true;

            SupermatterAnnouncement(uid, message, global);
            return;
        }

        // We are not taking consistent damage. Engis not needed.
        if (sm.Damage <= sm.DamageArchived)
            return;

        if (sm.Damage >= sm.WarningPoint)
        {
            message = Loc.GetString("supermatter-warning", ("integrity", integrity));
            if (sm.Damage >= sm.EmergencyPoint)
            {
                message = Loc.GetString("supermatter-emergency", ("integrity", integrity));
                global = true;
            }
        }
        SupermatterAnnouncement(uid, message, global);
    }

    /// <summary>
    ///     Help the SM announce something.
    /// </summary>
    /// <param name="global">If true, does the station announcement.</param>
    /// <param name="customSender">If set, the station announcement is sent under this name instead of the supermatter's.</param>
    public void SupermatterAnnouncement(EntityUid uid, string message, bool global = false, string? customSender = null)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (global)
        {
            var sender = customSender ?? Loc.GetString("supermatter-announcer");
            _chat.DispatchStationAnnouncement(uid, message, sender, colorOverride: Color.Yellow);
            return;
        }
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, hideChat: false, checkRadioPrefix: true);
    }

    /// <summary>
    ///     Returns the integrity rounded to hundreds, e.g. 100.00%
    /// </summary>
    public float GetIntegrity(SupermatterComponent sm)
    {
        var integrity = sm.Damage / sm.DelaminationPoint;
        integrity = (float) Math.Round(100 - integrity * 100, 2);
        integrity = integrity < 0 ? 0 : integrity;
        return integrity;
    }

    /// <summary>
    ///     Decide on how to delaminate.
    /// </summary>
    public DelamType ChooseDelamType(EntityUid uid, SupermatterComponent sm)
    {
        var mix = _atmosphere.GetContainingMixture(uid, true, true);

        if (mix is { })
        {
            var moles = mix.TotalMoles;

            if (moles >= sm.MolePenaltyThreshold)
                return DelamType.Singulo;
        }

        if (sm.Power >= sm.PowerPenaltyThreshold)
            return DelamType.Tesla;

        // TODO: add resonance cascade when there's crazy conditions, or a destabilizing crystal :godo:

        return DelamType.Explosion;
    }

    /// <summary>
    ///     Handle the end of the station.
    /// </summary>
    private void HandleDelamination(EntityUid uid, SupermatterComponent sm)
    {
        var xform = Transform(uid);

        _delamType = ChooseDelamType(uid, sm);

        if (!sm.Delamming)
        {
            sm.Delamming = true;
            HandleAnnouncements(uid, sm);
        }
        if (sm.Damage < sm.DelaminationPoint && sm.Delamming)
        {
            HandleAnnouncements(uid, sm);
            sm.Delamming = false;
            sm.DelamTimerAccumulator = 0;
            return;
        }

        sm.DelamTimerAccumulator++;

        if (sm.DelamTimer > sm.DelamTimerAccumulator)
            return;

        RaiseLocalEvent(new SupermatterDelaminatedEvent(uid));

        switch (_delamType)
        {
            case DelamType.Explosion:
            default:
                _explosion.TriggerExplosive(uid);
                break;

            case DelamType.Singulo:
                Spawn(sm.SingularityPrototypeId, xform.Coordinates);
                break;

            case DelamType.Tesla:
                Spawn(sm.TeslaPrototypeId, xform.Coordinates);
                break;

            case DelamType.Cascade:
                if (sm.SupermatterKudzuPrototypeId is { } kudzu)
                    Spawn(kudzu, xform.Coordinates);
                else
                    _explosion.TriggerExplosive(uid);
                break;
        }
    }

    private void HandleSoundLoop(EntityUid uid, SupermatterComponent sm)
    {
        var isAggressive = sm.Damage > sm.WarningPoint;
        var isDelamming = sm.Damage > sm.DelaminationPoint;

        if (!isAggressive && !isDelamming)
        {
            sm.AudioStream = _audio.Stop(sm.AudioStream);
            sm.SmSound = null;
            return;
        }

        var smSound = isDelamming ? SuperMatterSound.Delam : SuperMatterSound.Aggressive;

        if (sm.SmSound == smSound)
            return;

        sm.AudioStream = _audio.Stop(sm.AudioStream);
        sm.SmSound = smSound;
    }

    /// <summary>
    ///     Works out the alert status shown on supermatter monitoring consoles.
    /// </summary>
    private void UpdateStatus(SupermatterComponent sm)
    {
        var takingDamage = sm.Damage > sm.DamageArchived;

        sm.Status = sm switch
        {
            { Delamming: true } => SupermatterStatusType.Delaminating,
            _ when sm.Damage >= sm.EmergencyPoint => SupermatterStatusType.Emergency,
            _ when sm.Damage >= sm.WarningPoint && takingDamage => SupermatterStatusType.Danger,
            _ when sm.Damage >= sm.WarningPoint => SupermatterStatusType.Warning,
            _ when takingDamage || sm.Power > sm.PowerPenaltyThreshold => SupermatterStatusType.Caution,
            _ => SupermatterStatusType.Normal,
        };
    }

    #endregion

    #region Event Handlers

    private void OnCollideEvent(EntityUid uid, SupermatterComponent sm, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;

        if (args.OurEntity != uid)
            return;
        if (!args.OtherFixture.Hard && !HasComp<ProjectileComponent>(args.OtherEntity))
            return;

        // Stop immune entities from activating the sm.
        if (args.OtherBody.BodyType == BodyType.Static
            || HasComp<SupermatterImmuneComponent>(target)
            || _container.IsEntityInContainer(uid))
            return;

        if (!sm.Activated)
        {
            var activator = ToPrettyString(args.OtherEntity);
            var isMob = HasComp<MobStateComponent>(args.OtherEntity);
            var impact = isMob ? LogImpact.Extreme : LogImpact.High;

            _adminLog.Add(LogType.Supermatter, impact,
                $"{activator:actor} activated Supermatter {ToPrettyString(uid):subject} at {Transform(uid).Coordinates}");
            _chatmanager.SendAdminAlert(
                $"SUPERMATTER ACTIVATED BY {activator} AT {Transform(uid).Coordinates}");

            sm.Activated = true;
        }

        if (TryComp<AngeringProjectileComponent>(target, out var angering))
        {
            if (angering.IntegDamage is { } integDamage)
                sm.Damage = Math.Max(sm.Damage + integDamage, 0f);

            if (angering.EnergyDamage is { } energyDamage)
                sm.Power = Math.Max(sm.Power + energyDamage, 0f);
        }

        if (TryComp<SupermatterFoodComponent>(target, out var food))
            sm.Power += food.Energy;
        else if (TryComp<ProjectileComponent>(target, out var projectile))
            sm.Power += (float) projectile.Damage.GetTotal();
        else
            sm.Power++;

        sm.MatterPower += HasComp<MobStateComponent>(target) ? 200 : 0;

        if (!HasComp<ProjectileComponent>(target))
        {
            _adminLog.Add(LogType.Supermatter, LogImpact.Medium, $"Supermatter {ToPrettyString(uid)} has consumed {ToPrettyString(target)}");

            if (HasComp<ActorComponent>(target))
            {
                var consumedEv = new SupermatterConsumedEvent(uid);
                RaiseLocalEvent(target, ref consumedEv);
            }

            if (HasComp<ActorComponent>(target) || HasComp<MobStateComponent>(target))
                _chatmanager.SendAdminAlert($"Supermatter {ToPrettyString(uid)} has consumed {ToPrettyString(target)}");

            SpawnNextToOrDrop(AshPrototype, target);
            _audio.PlayPvs(sm.DustSound, uid);
        }
        QueueDel(target);
    }

    private void OnHandInteract(EntityUid uid, SupermatterComponent sm, ref InteractHandEvent args)
    {
        var target = args.User;

        if (HasComp<SupermatterImmuneComponent>(target))
            return;

        if (!sm.Activated)
            sm.Activated = true;

        sm.MatterPower += 200;

        var touchedEv = new SupermatterConsumedEvent(uid);
        RaiseLocalEvent(target, ref touchedEv);

        _adminLog.Add(LogType.Supermatter, LogImpact.Extreme, $"{ToPrettyString(target):actor} touched Supermatter {ToPrettyString(uid):subject} and was dusted");
        _chatmanager.SendAdminAlert($"Supermatter {ToPrettyString(uid)} has consumed {ToPrettyString(target)}");

        SpawnNextToOrDrop(AshPrototype, target);
        _audio.PlayPvs(sm.DustSound, uid);
        QueueDel(target);
    }

    private void OnItemInteract(EntityUid uid, SupermatterComponent sm, ref InteractUsingEvent args)
    {
        if (!HasComp<SupermatterImmuneComponent>(args.User))
            return;

        if (!sm.Activated)
            sm.Activated = true;

        if (sm.SliverRemoved)
            return;

        if (!_tool.HasQuality(args.Used, SlicingQuality))
            return;

        var dae = new DoAfterArgs(EntityManager, args.User, 30f, new SupermatterDoAfterEvent(), uid, uid, args.Used)
        {
            BreakOnDamage = true,
            BreakOnHandChange = false,
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true,
            RequireCanInteract = true,
        };

        if (!_doAfter.TryStartDoAfter(dae))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("supermatter-tamper-begin"), uid, args.User);
    }

    private void OnGetSliver(EntityUid uid, SupermatterComponent sm, ref SupermatterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || sm.SliverRemoved)
            return;

        args.Handled = true;

        // your criminal actions will not go unnoticed
        sm.Damage += sm.DelaminationPoint / 10;
        sm.DamageArchived += sm.DelaminationPoint / 10;
        sm.SliverRemoved = true;

        var integrity = GetIntegrity(sm).ToString("0.00");
        SupermatterAnnouncement(uid, Loc.GetString("supermatter-announcement-cc-tamper", ("integrity", integrity)), true, Loc.GetString("supermatter-announcer-central-command"));

        _adminLog.Add(LogType.Supermatter, LogImpact.High, $"{ToPrettyString(args.User):actor} cut a sliver off Supermatter {ToPrettyString(uid):subject}");

        Spawn(sm.SliverPrototypeId, _xform.GetMapCoordinates(args.User));
        _popup.PopupEntity(Loc.GetString("supermatter-tamper-end"), uid, args.User);

        if (sm.DelamTimer > 30f)
            sm.DelamTimer -= 10f;
    }

    private void OnExamine(EntityUid uid, SupermatterComponent sm, ref ExaminedEvent args)
    {
        // get all close and personal to it
        if (args.IsInDetailsRange)
        {
            args.PushMarkup(Loc.GetString("supermatter-examine-integrity", ("integrity", GetIntegrity(sm).ToString("0.00"))));
        }
    }

    #endregion

    #region SM events

    private string GetEventType(SupermatterComponent sm)
    {
        var events = _proto.Index(sm.SMLastAnger >= sm.HarshEventThreshold ? sm.HarshEvents : sm.NormalEvents);
        return events.Pick(_random);
    }

    private void RunEvent(EntityUid uid, SupermatterComponent sm, string eventId)
    {
        if (!_proto.TryIndex<SupermatterEventPrototype>(eventId, out var eventToRun))
        {
            Log.Error($"Supermatter {ToPrettyString(uid)} tried to run unknown supermatter event {eventId}");
            return;
        }

        if (eventToRun.Announcement != null) //shout over radio!
        {
            var message = Loc.GetString(eventToRun.Announcement.Value);
            _radioSystem.SendRadioMessage(uid, message, _proto.Index(sm.RadioChannel), uid);
        }

        _chatmanager.SendAdminAlert($"{eventToRun.ID} run by supermatter {ToPrettyString(uid)}");
        _adminLog.Add(LogType.Supermatter, LogImpact.Medium, $"Supermatter {ToPrettyString(uid)} ran event {eventToRun.ID}");

        if (eventToRun.GasToSpawn is { } gas) //If its a gas event - create the gas
        {
            var mix = _atmosphere.GetContainingMixture(uid, true, true);
            mix?.AdjustMoles(gas, 2000f);
        }
        else if (eventToRun.ProtoToSpawn is { } proto) //If its a spawn event - spawn what we want next to the SM
        {
            var coords = Transform(uid).Coordinates.Offset(new Vector2(0f, -1f));
            Spawn(proto, coords);
        }
        else if (eventToRun.Surge)
        {
            sm.Surge = true;
            _chatmanager.SendAdminAlert($"Supermatter {ToPrettyString(uid)} surge begun at time: {_gameTiming.CurTime.TotalMinutes}");
            sm.TimeLocked = _gameTiming.CurTime.TotalMinutes;
            sm.GasEfficiencyFactorChanged = true;
            sm.GasEfficiency = 0.30f;
            sm.RadiationOutputFactorChanged = true;
            sm.RadiationOutputFactor = 0.06f;
        }
    }

    public void AdjustSetpoints(SupermatterComponent sm)
    {
        if (sm.GasEfficiencyFactorChanged)
        {
            sm.GasEfficiency = StepTowards(sm.GasEfficiency, sm.GasEfficiencySetpoint);
            _adminLog.Add(LogType.Supermatter, LogImpact.Low,
                $"Supermatter gas efficiency factor adjusted to {sm.GasEfficiency}");

            if (sm.GasEfficiency == sm.GasEfficiencySetpoint)
                sm.GasEfficiencyFactorChanged = false;
        }
        if (sm.RadiationOutputFactorChanged)
        {
            sm.RadiationOutputFactor = StepTowards(sm.RadiationOutputFactor, sm.RadiationOutputFactorSetpoint);
            _adminLog.Add(LogType.Supermatter, LogImpact.Low,
                $"Supermatter radiation output factor adjusted to {sm.RadiationOutputFactor}");

            if (sm.RadiationOutputFactor == sm.RadiationOutputFactorSetpoint)
                sm.RadiationOutputFactorChanged = false;
        }
    }

    /// <summary>
    ///     Moves a value 1/50th of the way back to its setpoint each cycle, snapping once it is close enough.
    /// </summary>
    private static float StepTowards(float value, float setpoint)
    {
        var diff = (value - setpoint) / 50f;
        if (Math.Abs(diff) < 0.00001f)
            return setpoint;

        return value - diff;
    }

    #endregion
}
