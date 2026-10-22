using System.Linq;
using System.Numerics;
using Content.Server._ClawCommand.BloodCult.Runes;
using Content.Server.Chat.Systems;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared._ClawCommand.BloodCult.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.BloodCult.Items;

public sealed partial class BloodSpearSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedProjectileSystem _projectile = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodSpearComponent, EmbedEvent>(OnEmbed);
        SubscribeLocalEvent<BloodSpearComponent, GotEquippedHandEvent>(OnPickedUp);
        SubscribeLocalEvent<BloodSpearComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BloodCultistComponent, BloodSpearRecalledEvent>(OnRecall);
    }

    private void OnEmbed(Entity<BloodSpearComponent> spear, ref EmbedEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Embedded) || HasComp<BloodCultistComponent>(args.Embedded))
            return;

        // The halberd shatters into the victim, pinning them.
        _stun.TryUpdateParalyzeDuration(args.Embedded, spear.Comp.ParalyzeTime);
        QueueDel(spear);
    }

    private void OnPickedUp(Entity<BloodSpearComponent> spear, ref GotEquippedHandEvent args)
    {
        if (spear.Comp.Master != null || !TryComp<BloodCultistComponent>(args.User, out var cultist))
            return;

        spear.Comp.Master = args.User;
        spear.Comp.RecallAction = _actions.AddAction(args.User, spear.Comp.RecallActionId);
        cultist.BloodSpear = spear;
    }

    private void OnShutdown(Entity<BloodSpearComponent> spear, ref ComponentShutdown args)
    {
        if (spear.Comp.RecallAction is { } action)
            _actions.RemoveAction(action);

        if (TryComp<BloodCultistComponent>(spear.Comp.Master, out var cultist) && cultist.BloodSpear == spear.Owner)
            cultist.BloodSpear = null;
    }

    private void OnRecall(Entity<BloodCultistComponent> cultist, ref BloodSpearRecalledEvent args)
    {
        if (args.Handled || cultist.Comp.BloodSpear is not { } spearUid || !TryComp<BloodSpearComponent>(spearUid, out var spear))
            return;

        if (TryComp<EmbeddableProjectileComponent>(spearUid, out var embeddable) && embeddable.EmbeddedIntoUid != null)
            _projectile.EmbedDetach(spearUid, embeddable, cultist);

        _transform.SetCoordinates(spearUid, Transform(cultist).Coordinates);
        _transform.AttachToGridOrMap(spearUid);
        _hands.TryForcePickupAnyHand(cultist, spearUid);
        _audio.PlayPvs(spear.RecallAudio, spearUid);
        args.Handled = true;
    }

    public void DetachSpearFromMaster(Entity<BloodCultistComponent> cultist)
    {
        if (cultist.Comp.BloodSpear is not { } spearUid || !TryComp<BloodSpearComponent>(spearUid, out var spear))
            return;

        if (spear.RecallAction is { } action)
            _actions.RemoveAction(action);

        spear.RecallAction = null;
        spear.Master = null;
        cultist.Comp.BloodSpear = null;
    }
}

public sealed partial class ShuttleCurseSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleCurseComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<ShuttleCurseComponent> orb, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (Transform(orb).MapUid is not { } map)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-curse-cant-activate"), orb, args.User);
            return;
        }

        var provider = EnsureComp<ShuttleCurseProviderComponent>(map);
        if (provider.CurrentUses >= provider.MaxUses)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-curse-max-charges"), orb, args.User);
            return;
        }

        if (_emergencyShuttle.EmergencyShuttleArrived)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-curse-shuttle-arrived"), orb, args.User);
            return;
        }

        if (!_roundEnd.DelayShuttle(orb.Comp.DelayTime))
        {
            _popup.PopupEntity(Loc.GetString("shuttle-curse-shuttle-not-called"), orb, args.User);
            return;
        }

        var message = Loc.GetString(_random.Pick(orb.Comp.CurseMessages)) + " " +
                      Loc.GetString("shuttle-curse-success-global", ("time", orb.Comp.DelayTime.TotalMinutes));

        _chat.DispatchGlobalAnnouncement(message, Loc.GetString("shuttle-curse-system-failure"), colorOverride: Color.Gold);
        _popup.PopupEntity(Loc.GetString("shuttle-curse-success"), args.User, args.User);
        provider.CurrentUses++;

        _audio.PlayPvs(orb.Comp.ScatterSound, Transform(orb).Coordinates);
        QueueDel(orb);
    }
}

public sealed partial class VeilShifterSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VeilShifterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<VeilShifterComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnExamined(Entity<VeilShifterComponent> veil, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("veil-shifter-description", ("charges", veil.Comp.Charges)));
    }

    private void OnUseInHand(Entity<VeilShifterComponent> veil, ref UseInHandEvent args)
    {
        if (args.Handled || veil.Comp.Charges <= 0)
            return;

        args.Handled = true;
        if (!Teleport(veil, args.User))
            return;

        veil.Comp.Charges--;
        if (veil.Comp.Charges <= 0)
            _appearance.SetData(veil, GenericCultVisuals.State, false);
    }

    private bool Teleport(Entity<VeilShifterComponent> veil, EntityUid user)
    {
        var xform = Transform(user);
        var oldCoords = xform.Coordinates;
        var direction = _transform.GetWorldRotation(xform).ToWorldVec();

        Robust.Shared.Map.EntityCoordinates? found = null;
        for (var i = 0; i < veil.Comp.Attempts; i++)
        {
            var distance = _random.Next(veil.Comp.TeleportDistanceMin, veil.Comp.TeleportDistanceMax + 1);
            var world = _transform.GetWorldPosition(xform) + direction * distance;
            var coords = _transform.ToCoordinates(new Robust.Shared.Map.MapCoordinates(world, xform.MapID));

            if (_turf.GetTileRef(coords) is not { } tile || _turf.IsSpace(tile) || _turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                continue;

            found = _turf.GetTileCenter(tile);
            break;
        }

        if (found is not { } destination)
        {
            _popup.PopupEntity(Loc.GetString("veil-shifter-cant-teleport"), veil, user);
            return false;
        }

        EntityUid? pulled = null;
        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling is { } pulling &&
            TryComp<PullableComponent>(pulling, out var pullable))
        {
            pulled = pulling;
            _pulling.TryStopPull(pulling, pullable);
        }

        _transform.SetCoordinates(user, destination);
        if (pulled != null)
        {
            _transform.SetCoordinates(pulled.Value, destination);
            _pulling.TryStartPull(user, pulled.Value);
        }

        _audio.PlayPvs(veil.Comp.TeleportInSound, destination);
        _audio.PlayPvs(veil.Comp.TeleportOutSound, oldCoords);
        Spawn(veil.Comp.TeleportInEffect, destination);
        Spawn(veil.Comp.TeleportOutEffect, oldCoords);
        return true;
    }
}

public sealed partial class VoidTorchSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidTorchComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VoidTorchComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VoidTorchComponent, BloodCultOptionSelectedMessage>(OnCultistSelected);
    }

    private void OnMapInit(Entity<VoidTorchComponent> torch, ref MapInitEvent args)
    {
        _appearance.SetData(torch, GenericCultVisuals.State, torch.Comp.Charges > 0);
    }

    private void OnAfterInteract(Entity<VoidTorchComponent> torch, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || torch.Comp.Charges <= 0 || args.Target is not { } target ||
            target == torch.Owner || !HasComp<ItemComponent>(target) || !HasComp<BloodCultistComponent>(args.User))
            return;

        args.Handled = true;

        var entries = new List<BloodCultListEntry>();
        var query = EntityQueryEnumerator<BloodCultistComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (uid != args.User)
                entries.Add(new BloodCultListEntry(GetNetEntity(uid).ToString(), Name(uid)));
        }

        if (entries.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-rune-no-targets"), args.User, args.User);
            return;
        }

        torch.Comp.TargetItem = target;
        _ui.SetUiState(torch.Owner, BloodCultUiKey.VoidTorch, new BloodCultListState(entries));
        _ui.OpenUi(torch.Owner, BloodCultUiKey.VoidTorch, args.User);
    }

    private void OnCultistSelected(Entity<VoidTorchComponent> torch, ref BloodCultOptionSelectedMessage args)
    {
        if (!args.UiKey.Equals(BloodCultUiKey.VoidTorch))
            return;

        _ui.CloseUi(torch.Owner, BloodCultUiKey.VoidTorch);

        if (torch.Comp.Charges <= 0 || torch.Comp.TargetItem is not { } item || TerminatingOrDeleted(item) ||
            !NetEntity.TryParse(args.Id, out var netTarget) || !TryGetEntity(netTarget, out var target) ||
            !HasComp<BloodCultistComponent>(target))
            return;

        torch.Comp.TargetItem = null;

        // Out of the sender's hands first, then straight into the receiver's.
        if (_hands.IsHolding(args.Actor, item))
            _hands.TryDrop(args.Actor, item, checkActionBlocker: false);

        _transform.SetCoordinates(item, Transform(target.Value).Coordinates);
        _transform.AttachToGridOrMap(item);
        _hands.TryPickupAnyHand(target.Value, item, checkActionBlocker: false);

        _audio.PlayPvs(torch.Comp.TeleportSound, torch);
        Spawn("CultTeleportOutEffect", Transform(torch).Coordinates);

        torch.Comp.Charges--;
        if (torch.Comp.Charges <= 0)
            _appearance.SetData(torch, GenericCultVisuals.State, false);
    }
}

public sealed partial class TimedFactorySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedFactoryComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<TimedFactoryComponent, BloodCultOptionSelectedMessage>(OnSelected);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TimedFactoryComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out var factory, out var appearance))
        {
            if (factory.NextUse <= _timing.CurTime)
                _appearance.SetData(uid, GenericCultVisuals.State, true, appearance);
        }
    }

    private void OnOpenAttempt(Entity<TimedFactoryComponent> factory, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<BloodCultistComponent>(args.User))
        {
            args.Cancel();
            return;
        }

        var remaining = factory.Comp.NextUse - _timing.CurTime;
        if (remaining > TimeSpan.Zero)
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("timed-factory-cooldown", ("cooldown", Math.Ceiling(remaining.TotalSeconds))), factory, args.User);
            return;
        }

        var entries = new List<BloodCultRadialEntry>();
        foreach (var entry in factory.Comp.Entries)
        {
            if (_proto.TryIndex(entry, out var proto))
                entries.Add(new BloodCultRadialEntry(entry, proto.Name, entry));
        }

        _ui.SetUiState(factory.Owner, BloodCultUiKey.Factory, new BloodCultRadialState(entries));
    }

    private void OnSelected(Entity<TimedFactoryComponent> factory, ref BloodCultOptionSelectedMessage args)
    {
        if (!args.UiKey.Equals(BloodCultUiKey.Factory))
            return;

        _ui.CloseUi(factory.Owner, BloodCultUiKey.Factory);

        var proto = new EntProtoId(args.Id);
        if (factory.Comp.NextUse > _timing.CurTime || !factory.Comp.Entries.Contains(proto) ||
            !HasComp<BloodCultistComponent>(args.Actor))
            return;

        var product = Spawn(proto, Transform(args.Actor).Coordinates);
        _hands.TryPickupAnyHand(args.Actor, product);

        factory.Comp.NextUse = _timing.CurTime + factory.Comp.Cooldown;
        _appearance.SetData(factory, GenericCultVisuals.State, false);
    }
}

public sealed partial class BloodCultBarrierSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodCultBarrierComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<BloodCultBarrierComponent> barrier, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<RuneDrawerComponent>(args.Used) || !HasComp<BloodCultistComponent>(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cult-barrier-destroyed"), args.User, args.User);
        QueueDel(barrier);
    }
}

public sealed partial class BloodBoilProjectileSystem : EntitySystem
{
    [Dependency] private BloodCultItemSystem _cultItem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodBoilProjectileComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<BloodCultPassThroughCultComponent, PreventCollideEvent>(OnPassThroughCollide);
    }

    private void OnPassThroughCollide(Entity<BloodCultPassThroughCultComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<ThrownItemComponent>(ent) && _cultItem.IsCultMember(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnPreventCollide(Entity<BloodBoilProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OtherEntity != ent.Comp.Target)
            args.Cancelled = true;
    }
}

public sealed partial class WhetstoneSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WhetstoneComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<WhetstoneComponent> stone, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || stone.Comp.Uses <= 0 ||
            !TryComp<MeleeWeaponComponent>(target, out var melee) ||
            !HasComp<ItemComponent>(target) || // Don't sharpen the crew.
            _whitelist.IsWhitelistPass(stone.Comp.Blacklist, target) ||
            _whitelist.IsWhitelistFail(stone.Comp.Whitelist, target))
            return;

        args.Handled = true;

        var sharpened = false;
        foreach (var (type, increase) in stone.Comp.DamageIncrease.DamageDict)
        {
            if (!melee.Damage.DamageDict.TryGetValue(type, out var current) || current >= stone.Comp.MaximumIncrease)
                continue;

            melee.Damage.DamageDict[type] = Content.Shared.FixedPoint.FixedPoint2.Min(current + increase, stone.Comp.MaximumIncrease);
            sharpened = true;
        }

        if (!sharpened)
        {
            _popup.PopupEntity(Loc.GetString("whetstone-cant-sharpen"), target, args.User);
            return;
        }

        Dirty(target, melee);
        _audio.PlayPvs(stone.Comp.SharpenAudio, target);
        _popup.PopupEntity(Loc.GetString("whetstone-sharpened", ("weapon", target)), target, args.User);

        stone.Comp.Uses--;
        if (stone.Comp.Uses <= 0)
            _appearance.SetData(stone, GenericCultVisuals.State, false);
    }
}
