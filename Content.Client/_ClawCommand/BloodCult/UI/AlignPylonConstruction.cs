using Content.Shared._ClawCommand.BloodCult.Components;
using Robust.Client.Placement;
using Robust.Client.Placement.Modes;
using Robust.Shared.Map;

namespace Content.Client._ClawCommand.BloodCult.UI;

/// <summary>
/// Snap-to-grid placement that refuses spots too close to another pylon.
/// </summary>
public sealed class AlignPylonConstruction : SnapgridCenter
{
    private readonly IEntityManager _entityManager;
    private readonly EntityLookupSystem _lookup;

    private const float PylonLookupRange = 10f;

    public AlignPylonConstruction(PlacementManager pMan) : base(pMan)
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _lookup = _entityManager.System<EntityLookupSystem>();
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        if (!base.IsValidPosition(position))
            return false;

        var pylons = new HashSet<Entity<BloodCultPylonComponent>>();
        _lookup.GetEntitiesInRange(position, PylonLookupRange, pylons);
        return pylons.Count == 0;
    }
}
