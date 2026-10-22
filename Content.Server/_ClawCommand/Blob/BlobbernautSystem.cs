using System.Linq;
using Content.Shared._ClawCommand.Blob;
using Content.Shared._ClawCommand.Blob.Components;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._ClawCommand.Blob;

public sealed partial class BlobbernautSystem : SharedBlobbernautSystem
{
    [Dependency] private EntityLookupSystem _entityLookupSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;

    [Dependency] private ExplosionSystem _explosionSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EmpSystem _empSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TransformSystem _transform = default!;
    //private EntityQuery<MapGridComponent> _mapGridQuery;
    private EntityQuery<BlobTileComponent> _tileQuery;
    private EntityQuery<BlobCoreComponent> _coreQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobbernautComponent, MeleeHitEvent>(OnMeleeHit);

        //_mapGridQuery = GetEntityQuery<MapGridComponent>();
        _tileQuery = GetEntityQuery<BlobTileComponent>();
        _coreQuery = GetEntityQuery<BlobCoreComponent>();
    }


    private readonly HashSet<Entity<BlobTileComponent>> _entitySet = new();

    private static readonly ProtoId<DamageModifierSetPrototype> ChainCoatingModifiers = "BlobBlobbernautChainCoating";


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var blobFactoryQuery = EntityQueryEnumerator<BlobbernautComponent, MobStateComponent>();
        while (blobFactoryQuery.MoveNext(out var ent, out var comp, out var mobStateComponent))
        {
            if (_mobStateSystem.IsDead(ent,mobStateComponent))
                continue;

            comp.NextDamage += frameTime;

            if (comp.DamageFrequency > comp.NextDamage)
                continue;

            comp.NextDamage -= comp.DamageFrequency;

            if (TerminatingOrDeleted(comp.Factory))
            {
                TryChangeDamage("blobberaut-factory-destroy", ent, comp.Damage);
                continue;
            }

            var xform = Transform(ent);

            if (xform.GridUid == null)
                continue;

            var mapPos = _transform.ToMapCoordinates(xform.Coordinates);

            _entitySet.Clear();
            _entityLookupSystem.GetEntitiesInRange(mapPos.MapId, mapPos.Position, 1f, _entitySet);

            if(_entitySet.Count != 0)
                continue;

            TryChangeDamage("blobberaut-not-on-blob-tile", ent, comp.Damage);
        }
    }

    private void OnMeleeHit(EntityUid uid, BlobbernautComponent component, MeleeHitEvent args)
    {
        if (args.HitEntities.Count < 1)
            return;
        if (!_tileQuery.TryComp(component.Factory, out var blobTileComponent))
            return;
        if (!_coreQuery.TryComp(blobTileComponent.Core, out var blobCoreComponent))
            return;

        switch (blobCoreComponent.CurrentChem)
        {
            case BlobChemType.ExplosiveLattice:
                _explosionSystem.QueueExplosion(args.HitEntities.FirstOrDefault(), blobCoreComponent.BlobExplosive, 3, 1, 2, maxTileBreak: 0);
                break;

            case BlobChemType.ChainCoating:
            {
                _damageableSystem.SetDamageModifierSetId(uid, ChainCoatingModifiers);
                break;
            }

            case BlobChemType.ElectromagneticWeb:
            {
                var xform = Transform(args.HitEntities.FirstOrDefault());
                if (_random.Prob(0.15f))
                    _empSystem.EmpPulse(_transform.GetMapCoordinates(xform), 2f, 50f, TimeSpan.FromSeconds(3f));
                break;
            }
        }
    }

    private DamageSpecifier? TryChangeDamage(string msg, EntityUid ent, DamageSpecifier dmg)
    {
        _popup.PopupEntity(Loc.GetString(msg), ent, ent, PopupType.LargeCaution);
        return _damageableSystem.ChangeDamage(ent, dmg);
    }
}
