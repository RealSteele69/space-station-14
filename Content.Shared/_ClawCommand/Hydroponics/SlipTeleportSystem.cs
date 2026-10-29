using Content.Goobstation.Shared.Teleportation.Systems;
using Content.Shared.Slippery;
using Content.Shared.Teleportation;

namespace Content.Shared._ClawCommand.Hydroponics;

/// <summary>
/// Teleports whoever slips on an entity that has both <see cref="SlipperyComponent"/> and
/// <see cref="RandomTeleportComponent"/> - e.g. a bluespace banana peel.
/// </summary>
public sealed partial class SlipTeleportSystem : EntitySystem
{
    [Dependency] private SharedRandomTeleportSystem _randomTeleport = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomTeleportComponent, SlipEvent>(OnSlip);
    }

    private void OnSlip(Entity<RandomTeleportComponent> ent, ref SlipEvent args)
    {
        if (!HasComp<SlipperyComponent>(ent))
            return;

        _randomTeleport.RandomTeleport(args.Slipped, ent.Comp);
    }
}
