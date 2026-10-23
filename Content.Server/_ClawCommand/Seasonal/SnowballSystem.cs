using Content.Server.Popups;
using Content.Shared._ClawCommand.Seasonal;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Server._ClawCommand.Seasonal;

/// <summary>
/// Scooping snowballs from snowy tiles with a shovel, and packing loose snowballs into hard ones.
/// </summary>
public sealed partial class SnowballSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SnowScoopComponent, AfterInteractEvent>(OnScoopAfterInteract);
        SubscribeLocalEvent<SnowScoopComponent, SnowScoopDoAfterEvent>(OnScoopDoAfter);
        SubscribeLocalEvent<PackableSnowballComponent, UseInHandEvent>(OnPackUse);
        SubscribeLocalEvent<PackableSnowballComponent, SnowballPackDoAfterEvent>(OnPackDoAfter);
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

    private bool IsSnow(SnowScoopComponent comp, EntityCoordinates coords)
    {
        if (!_turf.TryGetTileRef(coords, out var tile))
            return false;

        var def = _turf.GetContentTileDefinition(tile.Value);
        return comp.Tiles.Contains(def.ID);
    }
}
