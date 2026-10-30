using System.Linq;
using System.Numerics;
using Content.Server._ClawCommand.BloodCult.Items;
using Content.Server._ClawCommand.BloodCult.Rule;
using Content.Server._ClawCommand.BloodCult.Spells;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Pinpointer;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Bible.Components;
using Content.Shared.Body.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Gibbing;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.GameTicking;

namespace Content.Server._ClawCommand.BloodCult.Runes;

/// <summary>
/// The effects of the individual cult runes.
/// </summary>
public sealed partial class CultRuneSystems : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private CultRuneBaseSystem _cultRune = default!;
    [Dependency] private BloodCultRuleSystem _cultRule = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private ServerNavMapSystem _navMap = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly LocId BloodCultTitle = "blood-cult-title";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultRuneOfferingComponent, TryInvokeCultRuneEvent>(OnOfferingInvoked);
        SubscribeLocalEvent<CultRuneEmpowerComponent, TryInvokeCultRuneEvent>(OnEmpowerInvoked);
        SubscribeLocalEvent<CultRuneReviveComponent, TryInvokeCultRuneEvent>(OnReviveInvoked);
        SubscribeLocalEvent<CultRuneBarrierComponent, TryInvokeCultRuneEvent>(OnBarrierInvoked);
        SubscribeLocalEvent<CultRuneBloodBoilComponent, TryInvokeCultRuneEvent>(OnBloodBoilInvoked);

        SubscribeLocalEvent<CultRuneSummonComponent, TryInvokeCultRuneEvent>(OnSummonInvoked);
        SubscribeLocalEvent<CultRuneSummonComponent, BloodCultOptionSelectedMessage>(OnSummonSelected);

        SubscribeLocalEvent<CultRuneApocalypseComponent, TryInvokeCultRuneEvent>(OnApocalypseInvoked);
        SubscribeLocalEvent<CultRuneApocalypseComponent, ApocalypseRuneDoAfterEvent>(OnApocalypseDoAfter);

        SubscribeLocalEvent<CultRuneRendingComponent, AfterRunePlacedEvent>(OnRendingPlaced);
        SubscribeLocalEvent<CultRuneRendingComponent, TryInvokeCultRuneEvent>(OnRendingInvoked);
        SubscribeLocalEvent<CultRuneRendingComponent, RendingRuneDoAfterEvent>(OnRendingDoAfter);
        SubscribeLocalEvent<CultRuneRendingComponent, ComponentShutdown>(OnRendingShutdown);
    }

    #region Offering

    private void OnOfferingInvoked(Entity<CultRuneOfferingComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        var targets = _cultRune.GetTargetsNearRune(rune, rune.Comp.OfferingRange, e => HasComp<BloodCultistComponent>(e));
        if (targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-no-targets"), rune, args.User);
            args.Cancel();
            return;
        }

        if (!TryOffer(rune, targets.First(), args.User, args.Invokers.Count))
            args.Cancel();
    }

    private bool TryOffer(Entity<CultRuneOfferingComponent> rune, EntityUid target, EntityUid user, int invokers)
    {
        // The dead are always sacrificed.
        if (_mobState.IsDead(target))
        {
            Sacrifice(rune, target);
            return true;
        }

        // Those who can't be swayed must be sacrificed alive, which takes more cultists.
        if (!_mind.TryGetMind(target, out _, out _) ||
            _cultRule.IsTarget(target) ||
            HasComp<BibleUserComponent>(target) ||
            HasComp<MindShieldComponent>(target))
        {
            if (invokers < rune.Comp.AliveSacrificeInvokersAmount)
            {
                _popup.PopupEntity(Loc.GetString("cult-offering-need-more-sacrifice", ("amount", rune.Comp.AliveSacrificeInvokersAmount)), rune, user);
                return false;
            }

            Sacrifice(rune, target);
            return true;
        }

        if (invokers < rune.Comp.ConvertInvokersAmount)
        {
            _popup.PopupEntity(Loc.GetString("cult-offering-need-more-convert", ("amount", rune.Comp.ConvertInvokersAmount)), rune, user);
            return false;
        }

        if (!_cultRule.Convert(target, user))
            return false;

        AddReviveCharges(rune, rune.Comp.ReviveChargesPerOffering);

        _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(2));
        if (TryComp<CuffableComponent>(target, out var cuffable))
        {
            foreach (var cuffs in cuffable.Container.ContainedEntities.ToList())
            {
                _cuffable.Uncuff(target, user, cuffs, cuffable);
            }
        }

        _statusEffects.TryRemoveStatusEffect(target, "StatusEffectMuted");
        _damageable.TryChangeDamage(target, rune.Comp.ConvertHealing, true, false, user);
        return true;
    }

    private void Sacrifice(Entity<CultRuneOfferingComponent> rune, EntityUid target)
    {
        AddReviveCharges(rune, rune.Comp.ReviveChargesPerOffering);
        _cultRule.RecordSacrifice();

        var coords = Transform(target).Coordinates;
        if (_mind.TryGetMind(target, out var mindId, out _))
        {
            var shard = Spawn(rune.Comp.SoulShardProto, coords);
            _mind.TransferTo(mindId, shard);
            _mind.UnVisit(mindId);
        }
        else
        {
            Spawn(rune.Comp.SoulShardGhostProto, coords);
        }

        _gibbing.Gib(target);
    }

    #endregion

    #region Empower

    private void OnEmpowerInvoked(Entity<CultRuneEmpowerComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        if (HasComp<BloodCultEmpoweredComponent>(args.User) || !HasComp<BloodCultistComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cult-buff-already-buffed"), args.User, args.User);
            args.Cancel();
            return;
        }

        AddComp<BloodCultEmpoweredComponent>(args.User);
    }

    #endregion

    #region Revive

    private void OnReviveInvoked(Entity<CultRuneReviveComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        if (EnsureReviveCharges(rune) is not { } charges || charges.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-revive-rune-no-charges"), args.User, args.User);
            args.Cancel();
            return;
        }

        var targets = _cultRune.GetTargetsNearRune(rune,
            rune.Comp.ReviveRange,
            e => !HasComp<BloodCultistComponent>(e) ||
                 !HasComp<DamageableComponent>(e) ||
                 !HasComp<MobThresholdsComponent>(e) ||
                 !HasComp<MobStateComponent>(e) ||
                 _mobState.IsAlive(e));

        if (targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-no-targets"), args.User, args.User);
            args.Cancel();
            return;
        }

        charges.Charges--;
        Revive(targets.First(), args.User, rune);
    }

    private void Revive(EntityUid target, EntityUid user, Entity<CultRuneReviveComponent> rune)
    {
        _damageable.TryChangeDamage(target, rune.Comp.Healing, true, false, user);

        if (_mobState.IsDead(target) &&
            _thresholds.TryGetThresholdForState(target, MobState.Dead, out var threshold) &&
            _damageable.GetTotalDamage(target) < threshold)
        {
            _mobState.ChangeMobState(target, MobState.Critical, origin: user);
        }

        if (!_mind.TryGetMind(target, out var mindId, out var mind) || mind.CurrentEntity == target ||
            !_player.TryGetSessionById(mind.UserId, out var session))
            return;

        _eui.OpenEui(new ReturnToBodyEui(mind, _mind, _player), session);
    }

    public void AddReviveCharges(EntityUid source, int amount)
    {
        if (EnsureReviveCharges(source) is { } charges)
            charges.Charges += amount;
    }

    private ReviveRuneChargesProviderComponent? EnsureReviveCharges(EntityUid source)
    {
        return Transform(source).MapUid is { } map ? EnsureComp<ReviveRuneChargesProviderComponent>(map) : null;
    }

    #endregion

    #region Barrier

    private void OnBarrierInvoked(Entity<CultRuneBarrierComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        Spawn(rune.Comp.SpawnPrototype, Transform(rune).Coordinates);
        QueueDel(rune);
    }

    #endregion

    #region Blood boil

    private void OnBloodBoilInvoked(Entity<CultRuneBloodBoilComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        var targets = _cultRune.GetTargetsNearRune(rune,
                rune.Comp.TargetsLookupRange,
                e => HasComp<BloodCultistComponent>(e) ||
                     !HasComp<BloodstreamComponent>(e) ||
                     _mobState.IsDead(e) ||
                     !_examine.InRangeUnOccluded(rune.Owner, e.Owner, rune.Comp.TargetsLookupRange))
            .Select(e => e.Owner)
            .ToList();

        if (targets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-blood-boil-rune-no-targets"), rune, args.User);
            args.Cancel();
            return;
        }

        for (var i = 0; i < rune.Comp.ProjectileCount; i++)
        {
            var target = _random.Pick(targets);
            if (TryComp<FlammableComponent>(target, out var flammable))
            {
                _flammable.AdjustFireStacks(target, rune.Comp.FireStacksPerProjectile, flammable);
                _flammable.Ignite(target, rune, flammable);
            }

            Shoot(rune, target);
        }

        _audio.PlayPvs(rune.Comp.ActivationSound, rune);
    }

    private void Shoot(Entity<CultRuneBloodBoilComponent> rune, EntityUid target)
    {
        var runePos = _transform.GetMapCoordinates(rune);
        var targetPos = _transform.GetMapCoordinates(target);
        var projectile = Spawn(rune.Comp.ProjectilePrototype, runePos);

        if (!HasComp<ProjectileComponent>(projectile))
            return;

        EnsureComp<BloodBoilProjectileComponent>(projectile).Target = target;
        _gun.ShootProjectile(projectile, targetPos.Position - runePos.Position, Vector2.Zero, rune, rune, rune.Comp.ProjectileSpeed);
    }

    #endregion

    #region Summon

    private void OnSummonInvoked(Entity<CultRuneSummonComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        if (_ui.IsUiOpen(rune.Owner, BloodCultUiKey.Summon))
        {
            args.Cancel();
            return;
        }

        var entries = new List<BloodCultListEntry>();
        var query = EntityQueryEnumerator<BloodCultistComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (args.Invokers.Contains(uid))
                continue;

            entries.Add(new BloodCultListEntry(GetNetEntity(uid).ToString(), Name(uid)));
        }

        if (entries.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-no-targets"), args.User, args.User);
            args.Cancel();
            return;
        }

        _ui.SetUiState(rune.Owner, BloodCultUiKey.Summon, new BloodCultListState(entries));
        _ui.OpenUi(rune.Owner, BloodCultUiKey.Summon, args.User);
    }

    private void OnSummonSelected(Entity<CultRuneSummonComponent> rune, ref BloodCultOptionSelectedMessage args)
    {
        if (!args.UiKey.Equals(BloodCultUiKey.Summon))
            return;

        _ui.CloseUi(rune.Owner, BloodCultUiKey.Summon);

        if (!NetEntity.TryParse(args.Id, out var netTarget) || !TryGetEntity(netTarget, out var target) ||
            !HasComp<BloodCultistComponent>(target))
            return;

        if (TryComp<PullableComponent>(target, out var pullable) && pullable.BeingPulled)
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-summon-being-pulled"), rune, args.Actor);
            return;
        }

        if (TryComp<CuffableComponent>(target, out var cuffable) && cuffable.CuffedHandCount > 0)
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-summon-cuffed"), rune, args.Actor);
            return;
        }

        _cultRune.TeleportTo(target.Value, rune);
        QueueDel(rune);
    }

    #endregion

    #region Apocalypse

    private void OnApocalypseInvoked(Entity<CultRuneApocalypseComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        if (rune.Comp.Used || rune.Comp.CurrentDoAfter != null)
        {
            args.Cancel();
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, rune.Comp.InvokeTime, new ApocalypseRuneDoAfterEvent(), rune)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out rune.Comp.CurrentDoAfter))
            args.Cancel();
    }

    private void OnApocalypseDoAfter(Entity<CultRuneApocalypseComponent> rune, ref ApocalypseRuneDoAfterEvent args)
    {
        rune.Comp.CurrentDoAfter = null;
        if (args.Cancelled || args.Handled || rune.Comp.Used)
            return;

        args.Handled = true;
        rune.Comp.Used = true;
        _appearance.SetData(rune, ApocalypseRuneVisuals.Used, true);

        _emp.EmpPulse(_transform.GetMapCoordinates(rune),
            rune.Comp.EmpRange,
            rune.Comp.EmpEnergyConsumption,
            rune.Comp.EmpDuration,
            args.User);

        foreach (var ev in rune.Comp.GuaranteedEvents)
        {
            _gameTicker.StartGameRule(ev);
        }

        // The weaker the cult, the harder the apocalypse lashes out.
        var threshold = MathF.Floor(_player.PlayerCount * rune.Comp.CultistsThreshold);
        if (_cultRule.GetTotalCultists() >= threshold || rune.Comp.PossibleEvents.Count == 0)
            return;

        var (randomEvent, repeat) = _random.Pick(rune.Comp.PossibleEvents);
        for (var i = 0; i < repeat; i++)
        {
            _gameTicker.StartGameRule(randomEvent);
        }
    }

    #endregion

    #region Dimensional rending

    private void OnRendingPlaced(Entity<CultRuneRendingComponent> rune, ref AfterRunePlacedEvent args)
    {
        var location = FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(rune.Owner));
        _chat.DispatchGlobalAnnouncement(Loc.GetString("cult-rending-drawing-finished", ("location", location)),
            Loc.GetString(BloodCultTitle),
            true,
            rune.Comp.FinishedDrawingAudio,
            Color.DarkRed);
    }

    private void OnRendingInvoked(Entity<CultRuneRendingComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        if (!_cultRule.IsObjectiveFinished())
        {
            _popup.PopupEntity(Loc.GetString("cult-rending-target-alive"), rune, args.User);
            args.Cancel();
            return;
        }

        if (rune.Comp.CurrentDoAfter != null)
        {
            _popup.PopupEntity(Loc.GetString("cult-rending-already-summoning"), rune, args.User);
            args.Cancel();
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, rune.Comp.SummonTime, new RendingRuneDoAfterEvent(), rune)
        {
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out rune.Comp.CurrentDoAfter))
        {
            args.Cancel();
            return;
        }

        var location = FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(rune.Owner));
        _chat.DispatchGlobalAnnouncement(Loc.GetString("cult-rending-started", ("location", location)),
            Loc.GetString(BloodCultTitle),
            false,
            colorOverride: Color.DarkRed);

        _appearance.SetData(rune, RendingRuneVisuals.Active, true);
        rune.Comp.AudioEntity = _audio.PlayGlobal(rune.Comp.SummonAudio, Filter.Broadcast(), false, AudioParams.Default.WithLoop(true))?.Entity;
    }

    private void OnRendingDoAfter(Entity<CultRuneRendingComponent> rune, ref RendingRuneDoAfterEvent args)
    {
        rune.Comp.CurrentDoAfter = null;
        rune.Comp.AudioEntity = _audio.Stop(rune.Comp.AudioEntity);
        _appearance.SetData(rune, RendingRuneVisuals.Active, false);

        if (args.Cancelled)
        {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("cult-rending-prevented"),
                Loc.GetString(BloodCultTitle),
                false,
                colorOverride: Color.DarkRed);
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;
        Spawn(rune.Comp.NarsiePrototype, _transform.GetMapCoordinates(rune));
        RaiseLocalEvent(new BloodCultNarsieSummonedEvent());
    }

    private void OnRendingShutdown(Entity<CultRuneRendingComponent> rune, ref ComponentShutdown args)
    {
        rune.Comp.AudioEntity = _audio.Stop(rune.Comp.AudioEntity);
    }

    #endregion
}
