using Content.Server.Holiday;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Seasonal;

/// <summary>
/// Adds limited-edition stock to a vending machine when it is set up during one of the stock's holidays.
/// </summary>
[RegisterComponent, Access(typeof(HolidayVendingStockSystem))]
public sealed partial class HolidayVendingStockComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<HolidayVendingStockPrototype>> Stock = new();
}

/// <summary>
/// A batch of seasonal items a vending machine carries while any of the listed holidays is being celebrated.
/// </summary>
[Prototype]
public sealed partial class HolidayVendingStockPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<HolidayPrototype>> Holidays = new();

    [DataField(required: true)]
    public Dictionary<EntProtoId, uint> Items = new();
}
