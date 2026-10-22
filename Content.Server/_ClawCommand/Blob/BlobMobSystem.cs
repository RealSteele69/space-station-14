using Content.Shared._ClawCommand.Blob;
using Content.Shared._ClawCommand.Blob.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server._ClawCommand.Blob;

public sealed partial class BlobMobSystem : SharedBlobMobSystem
{
    [Dependency] private DamageableSystem _damageableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobMobComponent, BlobMobGetPulseEvent>(OnPulsed);
    }

    private void OnPulsed(EntityUid uid, BlobMobComponent component, BlobMobGetPulseEvent args) =>
        _damageableSystem.TryChangeDamage(uid, component.HealthOfPulse);
}
