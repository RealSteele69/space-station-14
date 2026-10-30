using Content.Shared._ClawCommand.Lavaland.Salvage.Vendor;
using Content.Shared.Power;

namespace Content.Server._ClawCommand.Lavaland.Salvage.Vendor;

public sealed class ShopVendorSystem : SharedShopVendorSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShopVendorComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(Entity<ShopVendorComponent> ent, ref PowerChangedEvent args)
    {
        UpdateLight(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShopVendorComponent, TransformComponent>();
        var now = Timing.CurTime;
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            var ent = (uid, comp);
            var dirty = false;
            if (comp.Ejecting is {} ejecting && now > comp.NextEject)
            {
                Spawn(ejecting, xform.Coordinates);
                comp.Ejecting = null;
                dirty = true;
            }

            if (comp.Denying && now > comp.NextDeny)
            {
                comp.Denying = false;
                dirty = true;
            }

            if (dirty)
            {
                Dirty(uid, comp);
                UpdateLight(ent);
            }
        }
    }
}
