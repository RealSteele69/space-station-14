using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Lavaland.Salvage.Vendor;

[Serializable, NetSerializable]
public sealed class ShopVendorPurchaseMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}
