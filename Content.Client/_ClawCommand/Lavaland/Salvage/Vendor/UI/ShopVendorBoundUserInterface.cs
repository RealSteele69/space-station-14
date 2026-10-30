using Content.Shared._ClawCommand.Lavaland.Salvage.Vendor;
using Robust.Client.UserInterface;

namespace Content.Client._ClawCommand.Lavaland.Salvage.Vendor.UI;

public sealed class ShopVendorBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ShopVendorWindow? _window;

    public ShopVendorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ShopVendorWindow>();
        _window.SetEntity(Owner);
        _window.OpenCenteredLeft();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnItemSelected += index => SendMessage(new ShopVendorPurchaseMessage(index));
    }
}
