using Content.Shared._ClawCommand.Abilities;
using Content.Shared.Actions;
using Content.Shared.Climbing.Components;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server._ClawCommand.Abilities;

/// <summary>
///     Claw Command - Toggles <see cref="CrawlUnderObjectsComponent"/> sneak mode. Sneaking drops the
///     mid/high obstacle bits from the mob's collision mask so tables stop blocking it, slows it down,
///     and (client-side) draws it at small-mob depth so it renders under the table.
/// </summary>
public sealed partial class CrawlUnderObjectsSystem : SharedCrawlUnderObjectsSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MovementSpeedModifierSystem _movespeed = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrawlUnderObjectsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, ToggleCrawlingStateEvent>(OnAbilityToggle);
        SubscribeLocalEvent<CrawlUnderObjectsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnMapInit(Entity<CrawlUnderObjectsComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.ToggleHideAction != null || ent.Comp.ActionProto is not { } actionProto)
            return;

        _actions.AddAction(ent.Owner, ref ent.Comp.ToggleHideAction, actionProto);
    }

    /// <summary>
    ///     Whether standing back up here would wedge the mob inside a table.
    /// </summary>
    private bool IsOnCollidingTile(EntityUid uid)
    {
        if (_turf.GetTileRef(Transform(uid).Coordinates) is not { } tile)
            return false;

        return _turf.IsTileBlocked(tile, CollisionGroup.MobMask);
    }

    private bool IsClimbing(EntityUid uid)
    {
        return TryComp<ClimbingComponent>(uid, out var climbing) && climbing.IsClimbing;
    }

    private bool EnableSneakMode(Entity<CrawlUnderObjectsComponent> ent)
    {
        if (ent.Comp.Enabled || IsClimbing(ent))
            return false;

        ent.Comp.Enabled = true;
        Dirty(ent);
        RaiseLocalEvent(ent, new CrawlingUpdatedEvent(true));

        if (TryComp(ent, out FixturesComponent? fixtures))
        {
            foreach (var (key, fixture) in fixtures.Fixtures)
            {
                var newMask = (fixture.CollisionMask
                    & (int) ~CollisionGroup.HighImpassable
                    & (int) ~CollisionGroup.MidImpassable)
                    | (int) CollisionGroup.InteractImpassable;
                if (fixture.CollisionMask == newMask)
                    continue;

                ent.Comp.ChangedFixtures.Add((key, fixture.CollisionMask));
                _physics.SetCollisionMask(ent, key, fixture, newMask, manager: fixtures);
            }
        }

        return true;
    }

    private bool DisableSneakMode(Entity<CrawlUnderObjectsComponent> ent)
    {
        if (!ent.Comp.Enabled || IsOnCollidingTile(ent) || IsClimbing(ent))
            return false;

        ent.Comp.Enabled = false;
        Dirty(ent);
        RaiseLocalEvent(ent, new CrawlingUpdatedEvent(false));

        // Restore the normal collision masks.
        if (TryComp(ent, out FixturesComponent? fixtures))
        {
            foreach (var (key, originalMask) in ent.Comp.ChangedFixtures)
            {
                if (fixtures.Fixtures.TryGetValue(key, out var fixture))
                    _physics.SetCollisionMask(ent, key, fixture, originalMask, fixtures);
            }
        }

        ent.Comp.ChangedFixtures.Clear();
        return true;
    }

    private void OnAbilityToggle(Entity<CrawlUnderObjectsComponent> ent, ref ToggleCrawlingStateEvent args)
    {
        if (args.Handled)
            return;

        var result = ent.Comp.Enabled
            ? DisableSneakMode(ent)
            : EnableSneakMode(ent);

        _appearance.SetData(ent, SneakMode.Enabled, ent.Comp.Enabled);
        _movespeed.RefreshMovementSpeedModifiers(ent.Owner);

        args.Handled = result;
    }

    private void OnRefreshMovespeed(Entity<CrawlUnderObjectsComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Enabled)
            args.ModifySpeed(ent.Comp.SneakSpeedModifier, ent.Comp.SneakSpeedModifier);
    }
}
