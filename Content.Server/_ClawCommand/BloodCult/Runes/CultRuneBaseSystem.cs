using System.Linq;
using Content.Server._ClawCommand.BloodCult.Rule;
using Content.Server._ClawCommand.BloodCult.Spells;
using Content.Server.Chat.Systems;
using Content.Server.Chemistry.Components;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared._ClawCommand.BloodCult.Runes;
using Content.Shared._ClawCommand.BloodCult.Systems;
using Content.Shared.Bible.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.BloodCult.Runes;

/// <summary>
/// Scribing, erasing and invoking cult runes. What each rune does lives in its own system.
/// </summary>
public sealed partial class CultRuneBaseSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private BloodCultItemSystem _cultItem = default!;
    [Dependency] private BloodCultRuleSystem _cultRule = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Scribing
        SubscribeLocalEvent<RuneDrawerComponent, BeforeActivatableUIOpenEvent>(OnBeforeDrawerOpen);
        SubscribeLocalEvent<RuneDrawerComponent, BloodCultOptionSelectedMessage>(OnRuneSelected);
        SubscribeLocalEvent<BloodCultistComponent, BloodCultDrawRuneDoAfterEvent>(OnDrawRune);

        // Erasing
        SubscribeLocalEvent<CultRuneBaseComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CultRuneBaseComponent, BloodCultEraseRuneDoAfterEvent>(OnEraseDoAfter);
        SubscribeLocalEvent<CultRuneBaseComponent, StartCollideEvent>(OnStartCollide);

        // Invoking
        SubscribeLocalEvent<CultRuneBaseComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CultRuneBaseComponent, ExamineAttemptEvent>(OnExamineAttempt);
    }

    #region Scribing

    private void OnBeforeDrawerOpen(Entity<RuneDrawerComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        var entries = new List<BloodCultRadialEntry>();
        var totalCultists = _cultRule.GetTotalCultists();
        var targetDead = _cultRule.IsObjectiveFinished();

        foreach (var rune in _proto.EnumeratePrototypes<RuneSelectorPrototype>().OrderBy(r => r.Order))
        {
            if (rune.RequireTargetDead && !targetDead || rune.RequiredTotalCultists > totalCultists)
                continue;

            if (!_proto.TryIndex(rune.Prototype, out var runeProto))
                continue;

            entries.Add(new BloodCultRadialEntry(rune.ID, runeProto.Name, rune.Prototype));
        }

        _ui.SetUiState(ent.Owner, BloodCultUiKey.RuneDrawer, new BloodCultRadialState(entries));
    }

    private void OnRuneSelected(Entity<RuneDrawerComponent> ent, ref BloodCultOptionSelectedMessage args)
    {
        if (!args.UiKey.Equals(BloodCultUiKey.RuneDrawer))
            return;

        _ui.CloseUi(ent.Owner, BloodCultUiKey.RuneDrawer, args.Actor);

        if (!HasComp<BloodCultistComponent>(args.Actor) ||
            !_proto.TryIndex<RuneSelectorPrototype>(args.Id, out var rune) ||
            !CanDrawRune(args.Actor))
            return;

        if (rune.RequireTargetDead && !_cultRule.IsObjectiveFinished() ||
            rune.RequiredTotalCultists > _cultRule.GetTotalCultists())
            return;

        if (rune.RequireTargetDead && !_cultRule.CanDrawRendingRune(args.Actor))
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-cant-draw-rending"), args.Actor, args.Actor);
            return;
        }

        var time = rune.DrawTime;
        if (TryComp<BloodCultEmpoweredComponent>(args.Actor, out var empowered))
            time *= empowered.RuneTimeMultiplier;

        var doAfter = new DoAfterArgs(EntityManager, args.Actor, time, new BloodCultDrawRuneDoAfterEvent(rune.ID), args.Actor, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(ent.Comp.StartDrawingSound, args.Actor, AudioParams.Default.WithMaxDistance(3f));
    }

    private void OnDrawRune(Entity<BloodCultistComponent> ent, ref BloodCultDrawRuneDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !_proto.TryIndex(args.Rune, out var rune) || !CanDrawRune(ent))
            return;

        args.Handled = true;

        // Someone else may have used up the spot while this was being drawn.
        if (rune.RequireTargetDead && !_cultRule.CanDrawRendingRune(ent))
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-cant-draw-rending"), ent, ent);
            return;
        }

        DealDamage(ent, rune.DrawDamage);

        if (TryComp<RuneDrawerComponent>(args.Used, out var drawer))
            _audio.PlayPvs(drawer.EndDrawingSound, ent, AudioParams.Default.WithMaxDistance(3f));

        var runeEnt = SpawnRune(ent, rune.Prototype);
        if (TryComp<CultRuneBaseComponent>(runeEnt, out var runeBase) && runeBase.TriggerRendingMarkers)
            _cultRule.ConsumeNearestMarker(ent);

        var ev = new AfterRunePlacedEvent(ent);
        RaiseLocalEvent(runeEnt, ref ev);
    }

    #endregion

    #region Erasing

    private void OnInteractUsing(Entity<CultRuneBaseComponent> rune, ref InteractUsingEvent args)
    {
        if (args.Handled || !rune.Comp.CanBeErased)
            return;

        // A chaplain can scour a rune away with their holy book.
        if (TryComp<BibleComponent>(args.Used, out var bible) && HasComp<BibleUserComponent>(args.User))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cult-rune-erased"), rune, args.User);
            _audio.PlayPvs(bible.HealSound, args.User);
            QueueDel(rune);
            return;
        }

        if (!TryComp<RuneDrawerComponent>(args.Used, out var drawer) || !HasComp<BloodCultistComponent>(args.User))
            return;

        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager, args.User, drawer.EraseTime, new BloodCultEraseRuneDoAfterEvent(), rune, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cult-rune-started-erasing"), rune, args.User);
    }

    private void OnEraseDoAfter(Entity<CultRuneBaseComponent> rune, ref BloodCultEraseRuneDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cult-rune-erased"), rune, args.User);
        QueueDel(rune);
    }

    /// <summary>
    /// Holy water sprayed over a rune washes it away.
    /// </summary>
    private void OnStartCollide(Entity<CultRuneBaseComponent> rune, ref StartCollideEvent args)
    {
        if (!rune.Comp.CanBeErased ||
            !HasComp<VaporComponent>(args.OtherEntity) && !HasComp<SprayComponent>(args.OtherEntity) ||
            !TryComp<SolutionManagerComponent>(args.OtherEntity, out var manager))
            return;

        foreach (var (_, solution) in _solution.EnumerateSolutions((args.OtherEntity, manager)))
        {
            if (!solution.Comp.Solution.ContainsPrototype(rune.Comp.HolyWater))
                continue;

            QueueDel(rune);
            return;
        }
    }

    #endregion

    #region Invoking

    private void OnActivate(Entity<CultRuneBaseComponent> rune, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !_cultItem.IsCultMember(args.User) || HasComp<Content.Shared.Ghost.Components.GhostComponent>(args.User))
            return;

        if (!_transform.InRange(Transform(args.User).Coordinates, Transform(rune).Coordinates, rune.Comp.RuneActivationRange))
            return;

        args.Handled = true;

        var cultists = GatherCultists(rune, rune.Comp.RuneActivationRange);
        if (cultists.Count < rune.Comp.RequiredInvokers)
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-not-enough-cultists",
                    ("required", rune.Comp.RequiredInvokers),
                    ("current", cultists.Count)),
                rune,
                args.User);
            return;
        }

        var ev = new TryInvokeCultRuneEvent(args.User, cultists);
        RaiseLocalEvent(rune, ev);
        if (ev.Cancelled)
            return;

        foreach (var cultist in cultists)
        {
            DealDamage(cultist, rune.Comp.ActivationDamage);
            _chat.TrySendInGameICMessage(cultist, rune.Comp.InvokePhrase, rune.Comp.InvokeChatType, false, checkRadioPrefix: false);
        }
    }

    /// <summary>
    /// Only the cult (and the dead) can make sense of a rune.
    /// </summary>
    private void OnExamineAttempt(Entity<CultRuneBaseComponent> rune, ref ExamineAttemptEvent args)
    {
        if (!_cultItem.IsCultMember(args.Examiner))
            args.Cancel();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Every cult member standing around the rune.
    /// </summary>
    public HashSet<EntityUid> GatherCultists(EntityUid rune, float range)
    {
        var entities = _lookup.GetEntitiesInRange(Transform(rune).Coordinates, range);
        entities.RemoveWhere(entity => !HasComp<BloodCultistComponent>(entity) &&
                                       !(TryComp<BloodCultConstructComponent>(entity, out var construct) && !construct.Holy));
        return entities;
    }

    /// <summary>
    /// Every humanoid near the rune, minus whatever <paramref name="exclude"/> rejects.
    /// </summary>
    public HashSet<Entity<HumanoidProfileComponent>> GetTargetsNearRune(EntityUid rune,
        float range,
        Predicate<Entity<HumanoidProfileComponent>>? exclude = null)
    {
        var targets = _lookup.GetEntitiesInRange<HumanoidProfileComponent>(Transform(rune).Coordinates, range);
        if (exclude != null)
            targets.RemoveWhere(exclude);

        return targets;
    }

    /// <summary>
    /// Breaks any pulls involving <paramref name="target"/> so it can be moved.
    /// </summary>
    public void StopPulling(EntityUid target)
    {
        if (TryComp<PullableComponent>(target, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(target, pullable);

        if (TryComp<PullerComponent>(target, out var puller) && puller.Pulling is { } pulled &&
            TryComp<PullableComponent>(pulled, out var pulledComp))
            _pulling.TryStopPull(pulled, pulledComp);
    }

    /// <summary>
    /// Moves <paramref name="target"/> onto <paramref name="destination"/> with the cult's teleport effects.
    /// </summary>
    public void TeleportTo(EntityUid target, EntityUid destination)
    {
        var from = Transform(target).Coordinates;
        StopPulling(target);
        _transform.SetCoordinates(target, Transform(destination).Coordinates);
        _transform.AttachToGridOrMap(target);

        Spawn("CultTeleportOutEffect", from);
        Spawn("CultTeleportInEffect", Transform(destination).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/wand_teleport.ogg"), destination);
    }

    public void DealDamage(EntityUid target, DamageSpecifier? damage)
    {
        if (damage is null)
            return;

        var scaled = new DamageSpecifier(damage);
        if (TryComp<BloodCultEmpoweredComponent>(target, out var empowered))
            scaled *= empowered.RuneDamageMultiplier;

        _damageable.TryChangeDamage(target, scaled, true);
    }

    private EntityUid SpawnRune(EntityUid user, EntProtoId rune)
    {
        var coords = Transform(user).Coordinates;
        if (_turf.GetTileRef(coords) is { } tile)
            coords = _turf.GetTileCenter(tile);

        return Spawn(rune, coords);
    }

    private bool CanDrawRune(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid != null && _turf.GetTileRef(xform.Coordinates) is { } tile && !_turf.IsSpace(tile))
            return true;

        _popup.PopupEntity(Loc.GetString("cult-rune-cant-draw"), uid, uid);
        return false;
    }

    #endregion
}
