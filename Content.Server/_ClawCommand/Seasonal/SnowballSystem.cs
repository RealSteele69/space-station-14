using System.Collections.Generic;
using System.Linq;
using Content.Server.Popups;
using Content.Shared._ClawCommand.Seasonal;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Seasonal;

/// <summary>
/// Scooping snowballs from snowy tiles with a shovel, and packing loose snowballs into hard ones.
/// </summary>
public sealed partial class SnowballSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // Bare-handed alt-click scooping has no tool, so it's slower than scooping with a shovel.
    private static readonly TimeSpan BareHandScoopDelay = TimeSpan.FromSeconds(3);

    private static readonly ProtoId<ContentTileDefinition>[] BareHandSnowTiles =
    {
        "FloorSnow", "FloorAstroSnow", "PlatingSnow",
    };

    private static readonly EntProtoId BareHandSnowballSpawn = "Snowball";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SnowScoopComponent, AfterInteractEvent>(OnScoopAfterInteract);
        SubscribeLocalEvent<SnowScoopComponent, SnowScoopDoAfterEvent>(OnScoopDoAfter);
        SubscribeLocalEvent<PackableSnowballComponent, UseInHandEvent>(OnPackUse);
        SubscribeLocalEvent<PackableSnowballComponent, SnowballPackDoAfterEvent>(OnPackDoAfter);
        SubscribeLocalEvent<BareHandSnowScoopDoAfterEvent>(OnBareHandScoopDoAfter);

        // Alt-click an empty snowy tile with nothing usable in hand to scoop a snowball without a shovel.
        // Bound before the base interaction handler so a free hand doesn't fall through to its (otherwise no-op) ground click.
        CommandBinds.Builder
            .BindBefore(ContentKeyFunctions.AltActivateItemInWorld,
                new PointerInputCmdHandler(HandleBareHandAltClick),
                typeof(SharedInteractionSystem))
            .Register<SnowballSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<SnowballSystem>();
    }

    private bool HandleBareHandAltClick(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
    {
        if (session?.AttachedEntity is not { } user || !Exists(user))
            return false;

        // Only take over plain clicks on the ground; clicking on an actual entity keeps the usual alt-click behavior.
        if (!Deleted(uid))
            return false;

        // If they've got something usable in hand (e.g. a shovel), let the normal interaction pipeline handle it.
        if (_interaction.TryGetUsedEntity(user, out _))
            return false;

        if (!_actionBlocker.CanInteract(user, null))
            return false;

        if (!_interaction.InRangeUnobstructed(user, coords))
            return false;

        if (!IsSnow(coords, BareHandSnowTiles))
            return false;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, BareHandScoopDelay, new BareHandSnowScoopDoAfterEvent(), user)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });

        return true;
    }

    private void OnScoopAfterInteract(Entity<SnowScoopComponent> ent, ref AfterInteractEvent args)
    {
        // Clicking an entity is someone else's business (burial, botany trays...).
        if (args.Handled || !args.CanReach || args.Target != null)
            return;

        if (!IsSnow(ent.Comp, args.ClickLocation))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new SnowScoopDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnScoopDoAfter(Entity<SnowScoopComponent> ent, ref SnowScoopDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var snowball = Spawn(ent.Comp.Spawn, _transform.GetMapCoordinates(args.User));
        _hands.TryPickupAnyHand(args.User, snowball);
        _popup.PopupEntity(Loc.GetString("snowball-scooped"), args.User, args.User);
    }

    private void OnPackUse(Entity<PackableSnowballComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("snowball-packing"), args.User, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new SnowballPackDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnDropItem = true,
            NeedHand = true,
        });
    }

    private void OnPackDoAfter(Entity<PackableSnowballComponent> ent, ref SnowballPackDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        _hands.IsHolding(args.User, ent.Owner, out var hand);
        _hands.TryDrop(args.User, ent.Owner, checkActionBlocker: false, doDropInteraction: false);
        var packed = Spawn(ent.Comp.PackedInto, _transform.GetMapCoordinates(args.User));
        QueueDel(ent);

        if (hand == null || !_hands.TryPickup(args.User, packed, hand, checkActionBlocker: false))
            _hands.TryPickupAnyHand(args.User, packed, checkActionBlocker: false);
    }

    private void OnBareHandScoopDoAfter(BareHandSnowScoopDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var snowball = Spawn(BareHandSnowballSpawn, _transform.GetMapCoordinates(args.User));
        _hands.TryPickupAnyHand(args.User, snowball);
        _popup.PopupEntity(Loc.GetString("snowball-scooped"), args.User, args.User);
    }

    private bool IsSnow(SnowScoopComponent comp, EntityCoordinates coords)
    {
        return IsSnow(coords, comp.Tiles);
    }

    private bool IsSnow(EntityCoordinates coords, IReadOnlyList<ProtoId<ContentTileDefinition>> tiles)
    {
        if (!_turf.TryGetTileRef(coords, out var tile))
            return false;

        var def = _turf.GetContentTileDefinition(tile.Value);
        return tiles.Contains(def.ID);
    }
}
