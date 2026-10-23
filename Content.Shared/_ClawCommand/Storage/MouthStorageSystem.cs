using System.Linq;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Nutrition;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared._ClawCommand.Storage;

/// <summary>
///     Claw Command - Handles <see cref="MouthStorageComponent"/> cheek pouches.
///     Opening the pouch goes through the stock storage-implant action (<c>OpenStorageImplantEvent</c>),
///     which <see cref="Content.Shared.Storage.EntitySystems.SharedStorageSystem"/> handles on the pouch entity.
/// </summary>
public sealed partial class MouthStorageSystem : EntitySystem
{
    private const string MumbleAccent = "mumble";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MouthStorageComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MouthStorageComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<MouthStorageComponent, DisarmedEvent>(OnDisarmed);
        SubscribeLocalEvent<MouthStorageComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<MouthStorageComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MouthStorageComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<MouthStorageComponent, IngestionAttemptEvent>(OnIngestionAttempt);
    }

    /// <summary>
    ///     Whether anything is currently held in the pouch.
    /// </summary>
    public bool IsMouthBlocked(MouthStorageComponent component)
    {
        return TryComp<StorageComponent>(component.MouthId, out var storage)
            && storage.Container.ContainedEntities.Count > 0;
    }

    private void OnMapInit(Entity<MouthStorageComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Mouth = _container.EnsureContainer<Container>(ent, MouthStorageComponent.MouthContainerId);
        ent.Comp.Mouth.ShowContents = false;
        ent.Comp.Mouth.OccludesLight = false;

        if (ent.Comp.MouthId == null)
        {
            var mouth = Spawn(ent.Comp.MouthProto, new EntityCoordinates(ent, 0, 0));
            _container.Insert(mouth, ent.Comp.Mouth);
            ent.Comp.MouthId = mouth;
            Dirty(ent);
        }

        if (ent.Comp.OpenStorageAction is { } actionProto && ent.Comp.Action == null)
            _actions.AddAction(ent.Owner, ref ent.Comp.Action, actionProto, ent.Comp.MouthId.Value);
    }

    /// <summary>
    ///     Spits everything in the pouch out next to the mob.
    /// </summary>
    public void SpitOutContents(EntityUid uid, MouthStorageComponent component)
    {
        if (!TryComp<StorageComponent>(component.MouthId, out var storage))
            return;

        foreach (var item in storage.Container.ContainedEntities.ToArray())
        {
            // Not reparenting: the pouch itself sits in a container on the mob, so the default
            // reparent would just shove the item back inside the mob.
            if (_container.Remove(item, storage.Container, reparent: false))
                _transform.DropNextTo(item, uid);
        }
    }

    private void OnDowned(Entity<MouthStorageComponent> ent, ref DownedEvent args)
    {
        SpitOutContents(ent, ent.Comp);
    }

    private void OnDisarmed(Entity<MouthStorageComponent> ent, ref DisarmedEvent args)
    {
        SpitOutContents(ent, ent.Comp);
    }

    private void OnDamageDealt(Entity<MouthStorageComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Damage.GetTotal() < ent.Comp.SpitDamageThreshold)
            return;

        SpitOutContents(ent, ent.Comp);
    }

    // Other people can see that something is stuffed in this mob's cheeks.
    private void OnExamined(Entity<MouthStorageComponent> ent, ref ExaminedEvent args)
    {
        if (!IsMouthBlocked(ent.Comp))
            return;

        var subject = Identity.Entity(ent, EntityManager);
        args.PushMarkup(Loc.GetString("mouth-storage-examine-condition-occupied", ("entity", subject)));
    }

    // Talking with a full mouth comes out as mumbling.
    private void OnAccent(Entity<MouthStorageComponent> ent, ref AccentGetEvent args)
    {
        if (IsMouthBlocked(ent.Comp))
            args.Message = _replacement.ApplyReplacements(args.Message, MumbleAccent, ent.Owner);
    }

    // Nothing gets eaten or drunk past a full mouth.
    private void OnIngestionAttempt(Entity<MouthStorageComponent> ent, ref IngestionAttemptEvent args)
    {
        if (args.Cancelled || !TryComp<StorageComponent>(ent.Comp.MouthId, out var storage))
            return;

        if (storage.Container.ContainedEntities.Count == 0)
            return;

        args.Blocker = storage.Container.ContainedEntities[0];
        args.Cancelled = true;
    }
}
