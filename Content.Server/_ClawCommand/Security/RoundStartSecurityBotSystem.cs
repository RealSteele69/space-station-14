using Content.Server.Station.Events;
using Content.Shared.Roles;
using Content.Shared.Spawners.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._ClawCommand.Security;

/// <summary>
/// Claw Command - spawns a functional security bot ("officer Beepsky") at a random
/// SecurityOfficer spawn point on every station at round start, so there's always at least one
/// active enforcer patrolling even on rounds where nobody rolled Security.
/// </summary>
public sealed partial class RoundStartSecurityBotSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private static readonly ProtoId<JobPrototype> SecurityOfficerJob = "SecurityOfficer";
    private static readonly EntProtoId SecurityBotPrototype = "MobSecurityBot";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(ref StationPostInitEvent ev)
    {
        var grids = ev.Station.Comp.Grids;

        var spawnPoints = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var spawnPoint, out var xform))
        {
            if (xform.GridUid is not { } gridUid || !grids.Contains(gridUid))
                continue;

            if (spawnPoint.Job != SecurityOfficerJob)
                continue;

            spawnPoints.Add(xform.Coordinates);
        }

        if (spawnPoints.Count == 0)
            return;

        Spawn(SecurityBotPrototype, _random.Pick(spawnPoints));
    }
}
