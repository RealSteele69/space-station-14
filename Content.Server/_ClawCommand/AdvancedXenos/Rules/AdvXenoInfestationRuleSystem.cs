using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Station.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._ClawCommand.AdvancedXenos.Rules;

/// <summary>
/// Claw Command - /datum/event/xeno_infestation. Announces at the start, spawns when the event ends,
/// exactly like the original event's announceWhen/endWhen.
/// </summary>
public sealed partial class AdvXenoInfestationRuleSystem : StationEventSystem<AdvXenoInfestationRuleComponent>
{
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WeldableSystem _weldable = default!;

    protected override void Started(EntityUid uid, AdvXenoInfestationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
            return;

        var vents = new List<(EntityUid Vent, TransformComponent Xform)>();
        var query = EntityQueryEnumerator<GasVentPumpComponent, TransformComponent>();
        while (query.MoveNext(out var vent, out _, out var xform))
        {
            if (!xform.Anchored || _weldable.IsWelded(vent))
                continue;
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;
            vents.Add((vent, xform));
        }

        if (vents.Count == 0)
            return;

        // pick an "area": one random vent and every other vent near it
        var (centre, centreXform) = RobustRandom.Pick(vents);
        var centrePos = _transform.GetMapCoordinates(centre, centreXform);
        foreach (var (vent, xform) in vents)
        {
            var pos = _transform.GetMapCoordinates(vent, xform);
            if (pos.MapId == centrePos.MapId && (pos.Position - centrePos.Position).Length() <= component.AreaRadius)
                component.Vents.Add(xform.Coordinates);
        }

        component.LocationName = NearestBeacon(centrePos) ?? Loc.GetString("adv-xeno-infestation-default-location");

        // The original announcement still calls them lizards. It was copied from the roach event and never fixed.
        ChatSystem.DispatchStationAnnouncement(station.Value,
            Loc.GetString("adv-xeno-infestation-announcement", ("location", component.LocationName)),
            Loc.GetString("adv-xeno-infestation-sender"),
            colorOverride: Color.Gold);
    }

    protected override void Ended(EntityUid uid, AdvXenoInfestationRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (component.Vents.Count == 0)
        {
            Log.Info("Advanced xeno infestation found no valid vents to spawn at.");
            return;
        }

        foreach (var vent in component.Vents)
            Spawn(component.WeedNode, vent);

        var (prae, sentinel, drone, hunter) = FillSpawnTypes(component);
        Log.Info($"Advanced xeno infestation: {prae} praetorian(s), {sentinel} sentinel(s), {drone} drone(s), {hunter} hunter(s) in {component.LocationName}");

        for (var i = 0; i < prae; i++)
            Spawn(component.Praetorian, RobustRandom.Pick(component.Vents));
        for (var i = 0; i < sentinel; i++)
            Spawn(component.Sentinel, RobustRandom.Pick(component.Vents));
        for (var i = 0; i < drone; i++)
            Spawn(component.Drone, RobustRandom.Pick(component.Vents));
        for (var i = 0; i < hunter; i++)
            Spawn(component.Hunter, RobustRandom.Pick(component.Vents));
    }

    /// <summary>
    /// fill_spawn_type(). The mother, empress and queen tiers still eat their points even though their mob
    /// types were deleted upstream, so a big crew gets fewer, not more, aliens. Kept as the original has it.
    /// </summary>
    private (int Prae, int Sentinel, int Drone, int Hunter) FillSpawnTypes(AdvXenoInfestationRuleComponent component)
    {
        var points = CountPlayers() * component.Strength + component.AdditionalPlayers;
        var queenSpawning = false;
        int prae = 0, sentinel = 0, drone = 0, hunter = 0;

        // the hunter discount can loop for a while; the original trusts luck, we cap it
        var guard = 0;
        while (points > 0 && guard++ < 200)
        {
            if (points >= 20 * 1.5f)
            {
                points -= 20;
                queenSpawning = true;
                continue;
            }
            if (points >= 15 * 1.5f)
            {
                points -= 15;
                queenSpawning = true;
                continue;
            }
            if (points >= 10 * 1.5f)
            {
                points -= 10;
                queenSpawning = true;
                continue;
            }
            // rand(5) is "true" unless it rolls 0, so with a queen tier these are almost always discounted
            if (points >= 5)
            {
                prae++;
                points -= queenSpawning && RobustRandom.Next(0, 6) != 0 ? 3 : 5;
                continue;
            }
            if (points >= 4)
            {
                sentinel++;
                points -= queenSpawning && RobustRandom.Next(0, 11) != 0 ? 2 : 4;
                continue;
            }
            if (points >= 3)
            {
                drone++;
                points -= queenSpawning && RobustRandom.Next(0, 16) != 0 ? 1 : 3;
                continue;
            }

            hunter++;
            points -= queenSpawning && RobustRandom.Next(0, 6) != 0 ? 0 : 1;
        }

        return (prae, sentinel, drone, hunter);
    }

    /// <summary>get_player_count(): living humans on the station.</summary>
    private int CountPlayers()
    {
        var count = 0;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !HasComp<HumanoidProfileComponent>(mob) || _mobState.IsDead(mob))
                continue;
            if (!HasComp<StationMemberComponent>(Transform(mob).GridUid))
                continue;
            count++;
        }

        return count;
    }

    private string? NearestBeacon(Robust.Shared.Map.MapCoordinates coords)
    {
        string? best = null;
        var bestDist = float.MaxValue;
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out _, out var beacon, out var xform))
        {
            var pos = _transform.GetMapCoordinates(xform);
            if (pos.MapId != coords.MapId)
                continue;
            var dist = (pos.Position - coords.Position).Length();
            var text = beacon.Text ?? (beacon.DefaultText is { } loc ? Loc.GetString(loc) : null);
            if (dist >= bestDist || string.IsNullOrWhiteSpace(text))
                continue;
            best = text;
            bestDist = dist;
        }

        return best;
    }
}
