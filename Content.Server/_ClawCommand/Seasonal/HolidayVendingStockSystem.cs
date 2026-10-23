using Content.Server.Holiday;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Seasonal;

public sealed partial class HolidayVendingStockSystem : EntitySystem
{
    [Dependency] private HolidaySystem _holiday = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HolidayVendingStockComponent, MapInitEvent>(OnMapInit,
            after: [typeof(SharedVendingMachineSystem)]);
    }

    private void OnMapInit(Entity<HolidayVendingStockComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<VendingMachineComponent>(ent, out var vending))
            return;

        var changed = false;
        foreach (var stockId in ent.Comp.Stock)
        {
            if (!_proto.TryIndex(stockId, out var stock))
                continue;

            if (!stock.Holidays.Exists(h => _holiday.IsCurrentlyHoliday(h)))
                continue;

            foreach (var (id, amount) in stock.Items)
            {
                if (vending.Inventory.TryGetValue(id, out var existing))
                    existing.Amount += amount;
                else
                    vending.Inventory.Add(id, new VendingMachineInventoryEntry(InventoryType.Regular, id, amount));

                changed = true;
            }
        }

        if (changed)
            Dirty(ent, vending);
    }
}
