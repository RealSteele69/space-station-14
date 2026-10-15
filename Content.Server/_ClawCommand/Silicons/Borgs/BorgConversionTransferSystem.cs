using System.Linq;
using Content.Server.Construction;
using Content.Shared._ClawCommand.Silicons.Borgs;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;

namespace Content.Server._ClawCommand.Silicons.Borgs;

/// <summary>
/// CLAW COMMAND - applying a dogborg conversion kit swaps the cyborg for a brand new chassis entity via
/// construction, which only carries over construction-owned containers. Without this the MMI/positronic
/// brain, modules, power cell and law board stayed in the old chassis and were deleted with it.
/// </summary>
public sealed partial class BorgConversionTransferSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, EntityUid> _pendingTransfers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgChassisComponent, ConstructionChangeEntityEvent>(OnChangeEntity);
        SubscribeLocalEvent<BorgChassisComponent, AfterConstructionChangeEntityEvent>(OnAfterChangeEntity);
    }

    private void OnChangeEntity(Entity<BorgChassisComponent> ent, ref ConstructionChangeEntityEvent args)
    {
        // Raised on both the old and the new entity; only the old one knows what has to move.
        if (args.Old == ent.Owner)
            _pendingTransfers[args.New] = args.Old;
    }

    private void OnAfterChangeEntity(Entity<BorgChassisComponent> ent, ref AfterConstructionChangeEntityEvent args)
    {
        // The old chassis is only queued for deletion at this point, so its contents are still there.
        if (!_pendingTransfers.Remove(ent.Owner, out var old) || TerminatingOrDeleted(old))
            return;

        if (TryComp<BorgChassisComponent>(old, out var oldChassis))
        {
            Transfer(oldChassis.ModuleContainer, ent.Comp.ModuleContainer, ent, replaceExisting: false);
            Transfer(oldChassis.BrainContainer, ent.Comp.BrainContainer, ent, replaceExisting: false);
        }

        if (TryComp<PowerCellSlotComponent>(old, out var oldCell)
            && TryComp<PowerCellSlotComponent>(ent, out var newCell))
        {
            TransferById(old, oldCell.CellSlotId, ent, newCell.CellSlotId);
        }

        if (TryComp<BorgLawBoardComponent>(old, out var oldBoard)
            && TryComp<BorgLawBoardComponent>(ent, out var newBoard))
        {
            TransferById(old, oldBoard.SlotId, ent, newBoard.SlotId);
        }
    }

    private void TransferById(EntityUid from, string fromId, EntityUid to, string toId)
    {
        if (_container.TryGetContainer(from, fromId, out var fromContainer)
            && _container.TryGetContainer(to, toId, out var toContainer))
        {
            // The installed cell/board wins over whatever default the new chassis spawned with.
            Transfer(fromContainer, toContainer, to, replaceExisting: true);
        }
    }

    private void Transfer(BaseContainer from, BaseContainer to, EntityUid chassis, bool replaceExisting)
    {
        if (from.ContainedEntities.Count == 0)
            return;

        if (replaceExisting)
        {
            foreach (var existing in to.ContainedEntities.ToArray())
            {
                Del(existing);
            }
        }

        foreach (var item in from.ContainedEntities.ToArray())
        {
            _container.Remove(item, from, reparent: false, force: true);

            // Anything the new chassis won't take (module whitelist, slot limit) is dropped, never deleted.
            if (!_container.Insert(item, to))
                _transform.DropNextTo(item, chassis);
        }
    }
}
