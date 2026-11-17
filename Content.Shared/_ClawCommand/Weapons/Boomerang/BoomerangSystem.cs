// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Hands.EntitySystems;
using Content.Shared.Throwing;
using Robust.Shared.Map;

namespace Content.Shared._ClawCommand.Weapons.Boomerang;

public sealed partial class BoomerangSystem : EntitySystem
{
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private readonly List<(EntityUid Uid, EntityCoordinates Coords, float Speed, EntityUid? Thrower)> _toThrow = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoomerangComponent, LandEvent>(OnLanded);
        SubscribeLocalEvent<BoomerangComponent, ThrowEvent>(OnThrown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Throwing from inside LandEvent breaks the throw that just ended, so it's deferred to here.
        foreach (var (uid, coords, speed, thrower) in _toThrow)
        {
            if (!TerminatingOrDeleted(uid) && (thrower == null || !TerminatingOrDeleted(thrower)))
                _throwing.TryThrow(uid, coords, speed, user: thrower, recoil: false, playSound: false);
        }

        _toThrow.Clear();
    }

    private void OnThrown(Entity<BoomerangComponent> ent, ref ThrowEvent args)
    {
        if (ent.Comp.Thrower == null)
            SetThrower(ent, args.User);
    }

    private void OnLanded(Entity<BoomerangComponent> ent, ref LandEvent args)
    {
        if (ent.Comp.Thrower is not { } thrower)
            return;

        if (TerminatingOrDeleted(thrower) || ent.Comp.CurrentHops >= ent.Comp.MaxHops)
        {
            SetThrower(ent, null);
            return;
        }

        var throwerCoords = Transform(thrower).Coordinates;
        if (!Transform(ent).Coordinates.TryDistance(EntityManager, throwerCoords, out var distance))
        {
            SetThrower(ent, null);
            return;
        }

        if (distance < ent.Comp.PickupDistance)
        {
            // Failing the catch throws it with no user, so it can hit you.
            if (!_hands.TryPickup(thrower, ent))
                _toThrow.Add((ent, throwerCoords, ent.Comp.ReturnSpeed, null));

            SetThrower(ent, null);
            return;
        }

        _toThrow.Add((ent, throwerCoords, ent.Comp.ReturnSpeed, thrower));
        ent.Comp.CurrentHops++;
    }

    /// <summary>
    /// Sets the entity a boomerang should return to and resets the hop counter.
    /// </summary>
    public void SetThrower(Entity<BoomerangComponent> ent, EntityUid? newThrower)
    {
        ent.Comp.Thrower = newThrower;
        ent.Comp.CurrentHops = 0;
        Dirty(ent);
    }
}
