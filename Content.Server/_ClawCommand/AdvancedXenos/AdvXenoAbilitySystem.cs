using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.Actions;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle;
using Content.Shared.Chat;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - every xenomorph verb as an action: weeds, resin, eggs, acid, spit,
/// plasma transfer, evolution, leap, gut, tackle, psychic whisper, devour and regurgitate.
/// </summary>
public sealed partial class AdvXenoAbilitySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private AdvXenoLifeSystem _life = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private QuickDialogSystem _dialog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedCuffableSystem _cuffs = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private const string WallPlacementKey = "walls";

    private static readonly SoundSpecifier PierceSound = new SoundPathSpecifier("/Audio/Weapons/pierce.ogg");
    private static readonly SoundSpecifier LeapSound = new SoundPathSpecifier("/Audio/_ClawCommand/AdvancedXenos/Voice/hiss5.ogg");
    private static readonly SoundSpecifier ResinSound = new SoundPathSpecifier("/Audio/_ClawCommand/AdvancedXenos/Effects/attackblob.ogg");
    private static readonly SoundSpecifier DevourSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoPlantWeedsActionEvent>(OnPlantWeeds);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoSecreteResinActionEvent>(OnSecreteResin);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoLayEggActionEvent>(OnLayEgg);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoCorrosiveAcidActionEvent>(OnCorrosiveAcid);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoSpitActionEvent>(OnSpit);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoTransferPlasmaActionEvent>(OnTransferPlasma);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoEvolveActionEvent>(OnEvolve);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoLeapActionEvent>(OnLeap);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoGutActionEvent>(OnGut);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoTackleActionEvent>(OnTackle);
        SubscribeLocalEvent<AdvXenoHiveComponent, AdvXenoPsychicWhisperActionEvent>(OnPsychicWhisper);
        SubscribeLocalEvent<AdvXenoStomachComponent, AdvXenoDevourActionEvent>(OnDevour);
        SubscribeLocalEvent<AdvXenoStomachComponent, AdvXenoDevourDoAfterEvent>(OnDevourDoAfter);
        SubscribeLocalEvent<AdvXenoStomachComponent, AdvXenoRegurgitateActionEvent>(OnRegurgitate);
        SubscribeLocalEvent<AdvXenoStomachComponent, ComponentInit>(OnStomachInit);
        SubscribeLocalEvent<AdvXenoStomachComponent, Content.Shared.Mobs.MobStateChangedEvent>(OnStomachMobState);
        SubscribeLocalEvent<AdvXenoStomachComponent, EntityTerminatingEvent>(OnStomachTerminating);
        SubscribeLocalEvent<AdvXenoResinComponent, InteractHandEvent>(OnResinInteractHand);
    }

    #region Helpers

    /// <summary>Visible message: one line for onlookers, one for the user.</summary>
    private void Visible(EntityUid user, string others, string self, PopupType type = PopupType.Medium)
    {
        _popup.PopupEntity(others, user, Filter.PvsExcept(user), true, type);
        _popup.PopupEntity(self, user, user, type);
    }

    /// <summary>
    /// check_alien_ability "needs_foundation": anything but space.
    /// </summary>
    private bool HasFoundation(EntityUid user, EntityCoordinates coords)
    {
        if (_turf.GetTileRef(coords) is { } tile && !_turf.IsSpace(tile))
            return true;

        _popup.PopupEntity(Loc.GetString("adv-xeno-need-foundation"), user, user, PopupType.SmallCaution);
        return false;
    }

    private bool TryGetTileCenter(EntityCoordinates coords, out EntityCoordinates center)
    {
        center = coords;
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);
        center = _map.GridTileToLocal(gridUid, grid, tile);
        return true;
    }

    private bool TileHasSolid(EntityCoordinates center, EntityUid? ignore = null)
    {
        foreach (var ent in _lookup.GetEntitiesInRange(center, 0.35f, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (ent == ignore || HasComp<MapGridComponent>(ent) || HasComp<MapComponent>(ent))
                continue;
            if (TryComp<PhysicsComponent>(ent, out var body) && body.Hard && body.CanCollide)
                return true;
        }

        return false;
    }

    private bool TileHas<T>(EntityCoordinates center) where T : IComponent
    {
        foreach (var ent in _lookup.GetEntitiesInRange(center, 0.35f, LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (HasComp<T>(ent))
                return true;
        }

        return false;
    }

    private bool CanAct(EntityUid user)
    {
        return !_mobState.IsIncapacitated(user) && _blocker.CanInteract(user, null);
    }

    #endregion

    #region Builder abilities

    // /mob/living/carbon/human/proc/plant
    private void OnPlantWeeds(Entity<AdvXenoHiveComponent> ent, ref AdvXenoPlantWeedsActionEvent args)
    {
        if (args.Handled)
            return;

        var coords = Transform(ent).Coordinates;
        if (!TryGetTileCenter(coords, out var center) || !HasFoundation(ent, center))
            return;

        if (TileHas<AdvXenoWeedNodeComponent>(center))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-weeds-already"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!_life.TryUsePlasma(ent, args.Cost))
            return;

        args.Handled = true;
        // a node replaces the plain weeds under it
        foreach (var weed in _lookup.GetEntitiesInRange<AdvXenoWeedsComponent>(center, 0.35f))
            QueueDel(weed);

        Spawn(args.Node, center);
        Visible(ent, Loc.GetString("adv-xeno-weeds-planted", ("user", ent.Owner)), Loc.GetString("adv-xeno-weeds-planted-self"));
    }

    // /mob/living/carbon/human/proc/resin
    private void OnSecreteResin(Entity<AdvXenoHiveComponent> ent, ref AdvXenoSecreteResinActionEvent args)
    {
        if (args.Handled || !CanAct(ent))
            return;

        if (!TryGetTileCenter(args.Target, out var center) || !HasFoundation(ent, center))
            return;

        // measured to the click, like the action's own range; a tile centre can sit just past 1.5 from an off-centre xeno
        if (!_interaction.InRangeUnobstructed(ent.Owner, _transform.ToMapCoordinates(args.Target), 1.6f))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-too-far"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (args.Solid && TileHasSolid(center))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-resin-blocked"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!_life.TryUsePlasma(ent, args.Cost))
            return;

        args.Handled = true;
        Spawn(args.Prototype, center);
        _audio.PlayPvs(ResinSound, center);
        Visible(ent,
            Loc.GetString("adv-xeno-resin-shape", ("user", ent.Owner)),
            Loc.GetString("adv-xeno-resin-shape-self", ("shape", Loc.GetString(args.ShapeName))),
            PopupType.MediumCaution);
    }

    // /mob/living/carbon/human/proc/lay_egg
    private void OnLayEgg(Entity<AdvXenoHiveComponent> ent, ref AdvXenoLayEggActionEvent args)
    {
        if (args.Handled || !CanAct(ent))
            return;

        var coords = Transform(ent).Coordinates;
        if (!TryGetTileCenter(coords, out var center))
            return;

        if (TileHas<AdvXenoEggComponent>(center))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-egg-already"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!HasFoundation(ent, center) || !_life.TryUsePlasma(ent, args.Cost))
            return;

        args.Handled = true;
        Spawn(args.Egg, center);
        _popup.PopupEntity(Loc.GetString("adv-xeno-egg-laid", ("user", ent.Owner)), ent, PopupType.MediumCaution);
    }

    // /mob/living/carbon/human/proc/corrosive_acid
    private void OnCorrosiveAcid(Entity<AdvXenoHiveComponent> ent, ref AdvXenoCorrosiveAcidActionEvent args)
    {
        if (args.Handled || !CanAct(ent))
            return;

        var target = args.Target;
        // obj|turf in oview(1): only things lying in the world, never mobs, never something held or stored.
        if (HasComp<MobStateComponent>(target)
            || _container.IsEntityInContainer(target)
            || HasComp<AdvXenoAcidProofComponent>(target)
            || HasComp<MapGridComponent>(target)
            || HasComp<AdvXenoAcidedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-acid-cannot"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!_interaction.InRangeUnobstructed(ent.Owner, target, 1.5f))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-acid-too-far", ("target", target)), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!_life.TryUsePlasma(ent, args.Cost))
            return;

        args.Handled = true;
        var acid = Spawn(args.Acid, Transform(target).Coordinates);
        var comp = EnsureComp<AdvXenoAcidComponent>(acid);
        comp.Target = target;
        // Turf take twice as long to take down.
        comp.Strength = TryComp<PlacementReplacementComponent>(target, out var placement) && placement.Key == WallPlacementKey ? 8 : 4;
        comp.NextTick = _timing.CurTime;
        EnsureComp<AdvXenoAcidedComponent>(target);

        _popup.PopupEntity(Loc.GetString("adv-xeno-acid-applied", ("user", ent.Owner), ("target", target)), ent, PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):user} used corrosive acid on {ToPrettyString(target):target}");
    }

    #endregion

    #region Spit and plasma

    // /mob/living/carbon/human/proc/Spit (neurotoxin() / acidspit() toggles)
    private void OnSpit(Entity<AdvXenoHiveComponent> ent, ref AdvXenoSpitActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanAct(ent))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-spit-cannot"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!_life.TryUsePlasma(ent, args.Cost))
            return;

        args.Handled = true;
        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.ToMapCoordinates(args.Target);
        var direction = to.Position - from.Position;
        if (direction == Vector2.Zero)
            direction = _transform.GetWorldRotation(ent).ToWorldVec();

        var projectile = Spawn(args.Projectile, from);
        _gun.ShootProjectile(projectile, direction, _physics.GetMapLinearVelocity(ent), ent, ent, 20f);
        _audio.PlayPvs(PierceSound, ent, AudioParams.Default.WithVolume(-4f));

        var spitName = Loc.GetString(args.SpitName);
        Visible(ent,
            Loc.GetString("adv-xeno-spit", ("user", ent.Owner), ("spit", spitName)),
            Loc.GetString("adv-xeno-spit-self", ("spit", spitName)),
            PopupType.SmallCaution);
    }

    // /mob/living/carbon/human/proc/transfer_plasma
    private void OnTransferPlasma(Entity<AdvXenoHiveComponent> ent, ref AdvXenoTransferPlasmaActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!HasComp<AdvXenoPlasmaComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-transfer-no-vessel"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!_interaction.InRangeUnobstructed(ent.Owner, target, 1.5f))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-transfer-closer"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var user = ent.Owner;
        _dialog.OpenDialog<int>(actor.PlayerSession,
            Loc.GetString("adv-xeno-transfer-title", ("target", target)),
            Loc.GetString("adv-xeno-transfer-prompt"),
            amount =>
            {
                amount = Math.Abs(amount);
                if (amount == 0 || Deleted(user) || Deleted(target) || !TryComp<AdvXenoPlasmaComponent>(target, out var targetPlasma))
                    return;

                if (!_interaction.InRangeUnobstructed(user, target, 1.5f))
                {
                    _popup.PopupEntity(Loc.GetString("adv-xeno-transfer-closer"), user, user, PopupType.SmallCaution);
                    return;
                }

                if (!_life.TryUsePlasma(user, amount))
                    return;

                _life.AddPlasma((target, targetPlasma), amount);
                _popup.PopupEntity(Loc.GetString("adv-xeno-transfer-received", ("user", user), ("amount", amount)), target, target);
                _popup.PopupEntity(Loc.GetString("adv-xeno-transfer-sent", ("target", target), ("amount", amount)), user, user);
            });
    }

    #endregion

    #region Evolution

    // /mob/living/carbon/alien/verb/evolve and /mob/living/carbon/human/proc/evolve
    private void OnEvolve(Entity<AdvXenoHiveComponent> ent, ref AdvXenoEvolveActionEvent args)
    {
        if (args.Handled || _mobState.IsIncapacitated(ent))
            return;

        if (args.RequiresGrowth)
        {
            if (TryComp<CuffableComponent>(ent, out var cuffable) && _cuffs.IsCuffed((ent, cuffable), false))
            {
                _popup.PopupEntity(Loc.GetString("adv-xeno-evolve-cuffed"), ent, ent, PopupType.SmallCaution);
                return;
            }

            if (!TryComp<AdvXenoLarvaComponent>(ent, out var larva) || larva.Growth < larva.MaxGrowth)
            {
                _popup.PopupEntity(Loc.GetString("adv-xeno-evolve-not-grown"), ent, ent, PopupType.SmallCaution);
                return;
            }
        }

        if (args.UniqueQueen && _life.QueenExists(ent))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-evolve-queen-exists"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (args.Cost > 0 && !_life.TryUsePlasma(ent, args.Cost))
            return;

        args.Handled = true;
        Visible(ent, Loc.GetString("adv-xeno-evolve", ("user", ent.Owner)), Loc.GetString("adv-xeno-evolve-self"), PopupType.LargeCaution);
        TransformInto(ent, args.Into);
    }

    /// <summary>
    /// Replaces a xeno with a new form, carrying the mind over and dropping whatever it held or had eaten.
    /// </summary>
    public EntityUid TransformInto(EntityUid uid, EntProtoId into)
    {
        var xform = Transform(uid);
        var adult = Spawn(into, xform.Coordinates);
        _transform.SetLocalRotation(adult, xform.LocalRotation);

        foreach (var held in _hands.EnumerateHeld(uid).ToList())
            _hands.TryDrop(uid, held, checkActionBlocker: false);

        if (TryComp<AdvXenoStomachComponent>(uid, out var stomach))
            EmptyStomach((uid, stomach), false);

        if (_mind.TryGetMind(uid, out var mindId, out var mind))
            _mind.TransferTo(mindId, adult, mind: mind);

        QueueDel(uid);
        return adult;
    }

    #endregion

    #region Hunter / sentinel combat

    // /mob/living/carbon/human/proc/leap
    private void OnLeap(Entity<AdvXenoHiveComponent> ent, ref AdvXenoLeapActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanAct(ent) || _standing.IsDown(ent.Owner) || _buckle.IsBuckled(ent) || IsRestrained(ent))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-leap-cannot"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var target = args.Target;
        if (target == ent.Owner || HasComp<BorgChassisComponent>(target) || !HasComp<MobStateComponent>(target))
            return;

        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.GetMapCoordinates(target);
        var delta = to.Position - from.Position;
        if (from.MapId != to.MapId || delta.Length() > args.MaxDistance + 0.5f)
            return;

        args.Handled = true;
        // throw_at(get_step(get_turf(T), get_turf(src))): land on the tile in front of the target
        var landing = delta.Length() > 1f ? delta - Vector2.Normalize(delta) : Vector2.Zero;
        _popup.PopupEntity(Loc.GetString("adv-xeno-leap", ("user", ent.Owner), ("target", target)), ent, PopupType.MediumCaution);
        _audio.PlayPvs(LeapSound, ent);
        if (landing != Vector2.Zero)
            _throwing.TryThrow(ent, landing, 10f, ent, pushbackRatio: 0f, recoil: false, animated: false, playSound: false, doSpin: false);

        var leaping = EnsureComp<AdvXenoLeapingComponent>(ent);
        leaping.Target = target;
        leaping.ResolveAt = _timing.CurTime + TimeSpan.FromSeconds(0.5);
    }

    private void ResolveLeap(EntityUid uid, AdvXenoLeapingComponent leaping)
    {
        RemCompDeferred<AdvXenoLeapingComponent>(uid);
        var target = leaping.Target;
        if (Deleted(target) || !_interaction.InRangeUnobstructed(uid, target, 1.5f))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-leap-miss"), uid, uid, PopupType.SmallCaution);
            return;
        }

        _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(6));

        if (!_hands.TryGetEmptyHand(uid, out _))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-leap-hands-full"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (_pulling.TryStartPull(uid, target))
            _popup.PopupEntity(Loc.GetString("adv-xeno-leap-seize", ("user", uid), ("target", Identity.Entity(target, EntityManager))), uid, PopupType.MediumCaution);
    }

    private bool IsRestrained(EntityUid uid)
    {
        return TryComp<CuffableComponent>(uid, out var cuffable) && _cuffs.IsCuffed((uid, cuffable), false);
    }

    private bool TryGetAggressiveGrab(EntityUid uid, out EntityUid victim)
    {
        victim = default;
        if (!TryComp<PullerComponent>(uid, out var puller) || puller.Pulling is not { } pulled)
            return false;

        victim = pulled;
        return puller.GrabStage >= GrabStage.Soft;
    }

    // /mob/living/carbon/human/proc/gut
    private void OnGut(Entity<AdvXenoHiveComponent> ent, ref AdvXenoGutActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanAct(ent) || _standing.IsDown(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-cannot-now"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TryComp<PullerComponent>(ent, out var puller) || puller.Pulling == null)
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-gut-no-grab"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TryGetAggressiveGrab(ent, out var victim))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-gut-weak-grab"), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("adv-xeno-gut", ("user", ent.Owner), ("target", Identity.Entity(victim, EntityManager))), ent, PopupType.LargeCaution);
        _damage.TryChangeDamage(victim, new DamageSpecifier(_proto.Index(Slash), args.Damage), origin: ent);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(ent):user} gutted {ToPrettyString(victim):target}");

        if (_mobState.IsDead(victim))
            _gibbing.Gib(victim, user: ent);
    }

    // /mob/living/carbon/human/proc/tackle
    private void OnTackle(Entity<AdvXenoHiveComponent> ent, ref AdvXenoTackleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanAct(ent) || _standing.IsDown(ent.Owner) || _buckle.IsBuckled(ent) || IsRestrained(ent))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-tackle-cannot"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var target = args.Target;
        if (target == ent.Owner || HasComp<BorgChassisComponent>(target) || !HasComp<MobStateComponent>(target)
            || !_interaction.InRangeUnobstructed(ent.Owner, target, 1.5f))
            return;

        args.Handled = true;
        _audio.PlayPvs(PierceSound, ent);

        if (_random.Prob(0.75f))
        {
            // afflict_paralyze(20 * rand(0.5,3))
            _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(2 * _random.Next(1, 4)));
            _popup.PopupEntity(Loc.GetString("adv-xeno-tackle", ("user", ent.Owner), ("target", Identity.Entity(target, EntityManager))), ent, PopupType.MediumCaution);
        }
        else
        {
            _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(2 * _random.Next(2, 5)));
            _popup.PopupEntity(Loc.GetString("adv-xeno-tackle-fail", ("user", ent.Owner), ("target", Identity.Entity(target, EntityManager))), ent, PopupType.MediumCaution);
        }
    }

    // /mob/living/carbon/human/proc/psychic_whisper
    private void OnPsychicWhisper(Entity<AdvXenoHiveComponent> ent, ref AdvXenoPsychicWhisperActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        var target = args.Target;
        if (target == ent.Owner || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;
        var user = ent.Owner;
        _dialog.OpenDialog<string>(actor.PlayerSession,
            Loc.GetString("adv-xeno-whisper-title"),
            Loc.GetString("adv-xeno-whisper-prompt", ("target", target)),
            msg =>
            {
                msg = msg.Trim();
                if (msg.Length == 0 || Deleted(user) || Deleted(target))
                    return;
                if (msg.Length > 512)
                    msg = msg[..512];

                _adminLog.Add(LogType.Chat, LogImpact.Low, $"{ToPrettyString(user):user} psychic-whispered to {ToPrettyString(target):target}: {msg}");

                var color = Color.FromHex("#3bc43b");
                if (TryComp<ActorComponent>(target, out var targetActor))
                {
                    var heard = Loc.GetString("adv-xeno-whisper-heard", ("message", FormattedMessage.EscapeText(msg)));
                    _chat.ChatMessageToOne(ChatChannel.Telepathic, msg, heard, EntityUid.Invalid, false, targetActor.PlayerSession.Channel, color);
                }

                if (TryComp<ActorComponent>(user, out var userActor))
                {
                    var said = Loc.GetString("adv-xeno-whisper-said", ("message", FormattedMessage.EscapeText(msg)), ("target", target));
                    _chat.ChatMessageToOne(ChatChannel.Telepathic, msg, said, EntityUid.Invalid, false, userActor.PlayerSession.Channel, color);
                }
            });
    }

    #endregion

    #region Devour

    private void OnStomachInit(Entity<AdvXenoStomachComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, AdvXenoStomachComponent.ContainerId);
    }

    // /obj/item/grab/proc/devour (gluttonous = 2: any mob, 3 seconds)
    private void OnDevour(Entity<AdvXenoStomachComponent> ent, ref AdvXenoDevourActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!TryGetAggressiveGrab(ent, out var victim) || victim != target)
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-devour-need-grab"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (HasComp<AdvXenoHiveComponent>(target))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("adv-xeno-devour-start", ("user", ent.Owner), ("target", Identity.Entity(target, EntityManager))), ent, PopupType.LargeCaution);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.DevourTime, new AdvXenoDevourDoAfterEvent(), ent, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        });
    }

    private void OnDevourDoAfter(Entity<AdvXenoStomachComponent> ent, ref AdvXenoDevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        var stomach = _container.EnsureContainer<Container>(ent, AdvXenoStomachComponent.ContainerId);
        if (!_container.Insert(target, stomach))
            return;

        _audio.PlayPvs(DevourSound, ent);
        _popup.PopupEntity(Loc.GetString("adv-xeno-devoured", ("user", ent.Owner), ("target", Identity.Entity(target, EntityManager))), ent, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(ent):user} devoured {ToPrettyString(target):target}");
    }

    // /mob/living/carbon/human/proc/regurgitate
    private void OnRegurgitate(Entity<AdvXenoStomachComponent> ent, ref AdvXenoRegurgitateActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        EmptyStomach(ent, true);
    }

    public void EmptyStomach(Entity<AdvXenoStomachComponent> ent, bool message)
    {
        if (!_container.TryGetContainer(ent, AdvXenoStomachComponent.ContainerId, out var stomach) || stomach.ContainedEntities.Count == 0)
            return;

        _container.EmptyContainer(stomach, true, Transform(ent).Coordinates);
        if (message)
            _popup.PopupEntity(Loc.GetString("adv-xeno-regurgitate", ("user", ent.Owner)), ent, PopupType.LargeCaution);
    }

    private void OnStomachMobState(Entity<AdvXenoStomachComponent> ent, ref Content.Shared.Mobs.MobStateChangedEvent args)
    {
        if (args.NewMobState == Content.Shared.Mobs.MobState.Dead)
            EmptyStomach(ent, false);
    }

    private void OnStomachTerminating(Entity<AdvXenoStomachComponent> ent, ref EntityTerminatingEvent args)
    {
        EmptyStomach(ent, false);
    }

    #endregion

    // /obj/structure/alien/resin/attack_hand: the hive melts its own resin.
    private void OnResinInteractHand(Entity<AdvXenoResinComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !HasComp<AdvXenoHiveComponent>(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("adv-xeno-resin-melt", ("user", args.User), ("resin", ent.Owner)), ent, PopupType.Medium);
        _audio.PlayPvs(ResinSound, Transform(ent).Coordinates);
        QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var leapQuery = EntityQueryEnumerator<AdvXenoLeapingComponent>();
        while (leapQuery.MoveNext(out var uid, out var leaping))
        {
            if (now >= leaping.ResolveAt)
                ResolveLeap(uid, leaping);
        }
    }
}

/// <summary>Marks something already being eaten by corrosive acid.</summary>
[RegisterComponent]
public sealed partial class AdvXenoAcidedComponent : Component;
