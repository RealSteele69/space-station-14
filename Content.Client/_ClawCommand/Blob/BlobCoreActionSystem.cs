using Content.Client.Weapons.Melee;
using Content.Shared._ClawCommand.Blob;
using Content.Shared._ClawCommand.Blob.Events;
using Robust.Shared.Prototypes;

namespace Content.Client._ClawCommand.Blob;

public sealed partial class BlobCoreActionSystem : SharedBlobCoreActionSystem
{
    [Dependency] private MeleeWeaponSystem _meleeWeaponSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<BlobAttackEvent>(OnBlobAttack);
    }

    private static readonly EntProtoId Animation = "WeaponArcPunch";

    private void OnBlobAttack(BlobAttackEvent ev)
    {
        if(!TryGetEntity(ev.BlobEntity, out var user))
            return;

        _meleeWeaponSystem.DoLunge(user.Value, user.Value, Angle.Zero, ev.Position, Animation, false);
    }
}