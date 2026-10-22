using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Bed.Sleep;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - the "Life()" side of the xenomorphs: plasma regeneration, weed healing,
/// larva growth, alien naming ("alien drone (3)", queen/princess) and the death screech.
/// </summary>
public sealed partial class AdvXenoLifeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> Burn = "Burn";
    private static readonly ProtoId<DamageGroupPrototype> Airloss = "Airloss";
    private static readonly ProtoId<DamageGroupPrototype> Toxin = "Toxin";
    private static readonly ProtoId<DamageGroupPrototype>[] HealedGroups = { Brute, Burn, Airloss, Toxin };
    private const string PlasmaReagent = "Plasma";

    /// <summary>Per-caste alien_number, one counter per caste.</summary>
    private readonly Dictionary<string, int> _casteNumbers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoPlasmaComponent, MapInitEvent>(OnPlasmaInit);
        SubscribeLocalEvent<AdvXenoPlasmaComponent, ComponentShutdown>(OnPlasmaShutdown);
        SubscribeLocalEvent<AdvXenoLarvaComponent, MapInitEvent>(OnLarvaInit);
        SubscribeLocalEvent<AdvXenoLarvaComponent, ComponentShutdown>(OnLarvaShutdown);
        SubscribeLocalEvent<AdvXenoCasteComponent, MapInitEvent>(OnCasteInit);
        SubscribeLocalEvent<AdvXenoCasteComponent, PlayerAttachedEvent>(OnCastePlayerAttached);
        SubscribeLocalEvent<AdvXenoHiveComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _casteNumbers.Clear());
    }

    #region Init / naming

    private void OnPlasmaInit(Entity<AdvXenoPlasmaComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + ent.Comp.TickInterval;
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnPlasmaShutdown(Entity<AdvXenoPlasmaComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnLarvaInit(Entity<AdvXenoLarvaComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + ent.Comp.TickInterval;
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert);
        // /mob/living/carbon/alien/Initialize: name = "[initial(name)] ([rand(1, 1000)])"
        _meta.SetEntityName(ent, Loc.GetString("adv-xeno-larva-name", ("number", _random.Next(1, 1001))));
        UpdateLarvaVisuals(ent);
    }

    private void OnLarvaShutdown(Entity<AdvXenoLarvaComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnCasteInit(Entity<AdvXenoCasteComponent> ent, ref MapInitEvent args)
    {
        // /datum/species/xenos/handle_post_spawn: alien_number++, "alien [caste_name] ([alien_number])"
        var number = _casteNumbers.GetValueOrDefault(ent.Comp.Caste) + 1;
        _casteNumbers[ent.Comp.Caste] = number;
        _meta.SetEntityName(ent, Loc.GetString("adv-xeno-caste-name", ("caste", ent.Comp.Caste), ("number", number)));
        if (ent.Comp.Queen)
            UpdateQueenName(ent);
    }

    // /datum/species/xenos/queen/handle_login_special: only one official queen at a time.
    private void OnCastePlayerAttached(Entity<AdvXenoCasteComponent> ent, ref PlayerAttachedEvent args)
    {
        if (ent.Comp.Queen)
            UpdateQueenName(ent);
    }

    private void UpdateQueenName(Entity<AdvXenoCasteComponent> ent)
    {
        var number = _casteNumbers.GetValueOrDefault(ent.Comp.Caste);
        var title = QueenExists(ent) ? "princess" : "queen";
        _meta.SetEntityName(ent, Loc.GetString("adv-xeno-caste-name", ("caste", title), ("number", number)));
    }

    /// <summary>
    /// alien_queen_exists(): a living, conscious queen that someone is actually playing.
    /// </summary>
    public bool QueenExists(EntityUid? ignore = null)
    {
        var query = EntityQueryEnumerator<AdvXenoCasteComponent, ActorComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var caste, out _, out var mob))
        {
            if (!caste.Queen || uid == ignore || mob.CurrentState != MobState.Alive)
                continue;
            if (HasComp<SleepingComponent>(uid))
                continue;
            return true;
        }

        return false;
    }

    #endregion

    private void OnMobStateChanged(Entity<AdvXenoHiveComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (TryComp<AdvXenoCasteComponent>(ent, out var caste))
        {
            _popup.PopupEntity(Loc.GetString(caste.DeathMessage, ("xeno", ent.Owner)), ent, PopupType.MediumCaution);
            _audio.PlayPvs(caste.DeathSound, ent);
        }
        else if (HasComp<AdvXenoLarvaComponent>(ent))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-death-message", ("xeno", ent.Owner)), ent, PopupType.MediumCaution);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var plasmaQuery = EntityQueryEnumerator<AdvXenoPlasmaComponent>();
        while (plasmaQuery.MoveNext(out var uid, out var plasma))
        {
            if (now < plasma.NextTick)
                continue;
            plasma.NextTick = now + plasma.TickInterval;
            if (_mobState.IsDead(uid))
                continue;
            TickPlasma((uid, plasma));
        }

        var larvaQuery = EntityQueryEnumerator<AdvXenoLarvaComponent>();
        while (larvaQuery.MoveNext(out var uid, out var larva))
        {
            if (now < larva.NextTick)
                continue;
            larva.NextTick = now + larva.TickInterval;
            if (_mobState.IsDead(uid))
                continue;
            TickLarva((uid, larva));
        }
    }

    #region Plasma

    private void TickPlasma(Entity<AdvXenoPlasmaComponent> ent)
    {
        var gain = ent.Comp.Regen;

        // owner.bloodstr.has_reagent("phoron"): adjust_plasma(4)
        if (TryComp<BloodstreamComponent>(ent, out var blood)
            && _solution.ResolveSolution(ent.Owner, blood.BloodSolutionName, ref blood.BloodSolution, out var bloodSol)
            && bloodSol.GetTotalPrototypeQuantity(PlasmaReagent) > 0
            && blood.BloodSolution is { } bloodEnt)
        {
            _solution.RemoveReagent(bloodEnt, PlasmaReagent, 1);
            gain += ent.Comp.ReagentPlasmaRate;
        }

        // handle_environment_special: phoron in the air or weeds underfoot. Healing takes priority over plasma.
        if (IsOnWeedsOrPlasma(ent) && !Regenerate(ent, ent.Comp.WeedsHealRate))
            gain += ent.Comp.WeedsPlasmaRate;

        AddPlasma(ent, gain);
    }

    public void AddPlasma(Entity<AdvXenoPlasmaComponent> ent, int amount)
    {
        var old = ent.Comp.Plasma;
        ent.Comp.Plasma = Math.Clamp(ent.Comp.Plasma + amount, 0, ent.Comp.MaxPlasma);
        if (old != ent.Comp.Plasma)
            Dirty(ent);
    }

    /// <summary>
    /// check_alien_ability: refuses with a message when the vessel is short.
    /// </summary>
    public bool TryUsePlasma(EntityUid uid, int cost, bool popup = true)
    {
        if (!TryComp<AdvXenoPlasmaComponent>(uid, out var plasma))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("adv-xeno-no-vessel"), uid, uid, PopupType.SmallCaution);
            return false;
        }

        if (plasma.Plasma < cost)
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("adv-xeno-not-enough-plasma"), uid, uid, PopupType.SmallCaution);
            return false;
        }

        AddPlasma((uid, plasma), -cost);
        return true;
    }

    /// <summary>
    /// /datum/species/xenos/proc/regenerate. Heals every damage group at once, a third as fast unless lying down.
    /// </summary>
    public bool Regenerate(EntityUid uid, float rate)
    {
        if (!HasComp<DamageableComponent>(uid))
            return false;

        if (!_standing.IsDown(uid) && !HasComp<SleepingComponent>(uid))
            rate /= 3f;

        if (!HealGroups(uid, rate))
            return false;

        if (_random.Prob(0.05f))
            _popup.PopupEntity(Loc.GetString("adv-xeno-regen-soothing"), uid, uid);
        return true;
    }

    /// <summary>Heals up to <paramref name="rate"/> from each of brute, burn, oxy and tox. True if anything healed.</summary>
    private bool HealGroups(EntityUid uid, float rate)
    {
        var healed = false;
        foreach (var group in HealedGroups)
        {
            if (!_damage.HealEvenly(uid, -rate, group).Empty)
                healed = true;
        }

        return healed;
    }

    public bool IsOnWeeds(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var ent in _map.GetAnchoredEntities((gridUid, grid), tile))
        {
            if (HasComp<AdvXenoWeedsComponent>(ent))
                return true;
        }

        return false;
    }

    public bool IsOnWeedsOrPlasma(EntityUid uid)
    {
        if (IsOnWeeds(uid))
            return true;

        var air = _atmos.GetContainingMixture(uid, excite: false);
        return air != null && air.GetMoles(Gas.Plasma) > 0;
    }

    #endregion

    #region Larva

    private void TickLarva(Entity<AdvXenoLarvaComponent> ent)
    {
        var old = ent.Comp.Growth;

        // BiologicalLife: update_progression() every tick.
        if (ent.Comp.Growth < ent.Comp.MaxGrowth)
            ent.Comp.Growth++;

        // larva/handle_environment: phoron or weeds grow and heal it again.
        if (IsOnWeedsOrPlasma(ent))
        {
            if (ent.Comp.Growth < ent.Comp.MaxGrowth)
                ent.Comp.Growth++;

            HealGroups(ent, 1f);
        }

        if (old == ent.Comp.Growth)
            return;

        Dirty(ent);
        UpdateLarvaVisuals(ent);

        if (ent.Comp.Growth >= ent.Comp.MaxGrowth && !ent.Comp.GrownNotified)
        {
            ent.Comp.GrownNotified = true;
            if (TryComp<ActorComponent>(ent, out var actor))
            {
                var msg = Loc.GetString("adv-xeno-larva-grown");
                _chat.ChatMessageToOne(ChatChannel.Notifications, msg, msg, EntityUid.Invalid, false, actor.PlayerSession.Channel, Color.FromHex("#8f4fcf"));
            }
        }
    }

    /// <summary>
    /// /mob/living/carbon/alien/update_icons: larva0 below 25% growth, larva1 below 75%, then larva2.
    /// </summary>
    private void UpdateLarvaVisuals(Entity<AdvXenoLarvaComponent> ent)
    {
        var stage = 0;
        if (ent.Comp.Growth > ent.Comp.MaxGrowth * 0.75f)
            stage = 2;
        else if (ent.Comp.Growth > ent.Comp.MaxGrowth * 0.25f)
            stage = 1;

        _appearance.SetData(ent, AdvXenoLarvaVisuals.Stage, stage);
    }

    #endregion
}
