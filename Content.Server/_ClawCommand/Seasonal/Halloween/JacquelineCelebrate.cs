using Content.Server.Holiday;
using Content.Server.Holiday.Interfaces;
using Content.Server.Spawners.Components;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Spawners.Components;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Jacqueline's week: for a stretch of days around Halloween (see the JacquelineWeek holiday),
/// Jacqueline the Pumpqueen is dropped on a random crew (Passenger/Assistant) spawn point at round
/// start, instead of only existing as something admins have to spawn in by hand.
/// </summary>
[DataDefinition]
public sealed partial class JacquelineCelebrate : IHolidayCelebrate
{
    [DataField]
    public EntProtoId Mob = "MobJacquelinePumpqueen";

    /// <summary>
    /// Jobs whose spawn points are safe/public enough to drop Jacqueline on.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new() { "Passenger" };

    public void Celebrate(HolidayPrototype holiday)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var random = IoCManager.Resolve<IRobustRandom>();

        var candidates = new List<EntityCoordinates>();
        var points = entMan.EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (points.MoveNext(out _, out var point, out var xform))
        {
            if (point.SpawnType != SpawnPointType.Job)
                continue;

            if (point.Job is not { } job || !Jobs.Contains(job))
                continue;

            candidates.Add(xform.Coordinates);
        }

        if (candidates.Count == 0)
            return;

        entMan.SpawnEntity(Mob, random.Pick(candidates));
    }
}
