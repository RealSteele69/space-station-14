using Content.Server.Holiday;
using Content.Server.Holiday.Interfaces;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Dataset;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Station.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Spooky season celebration: at round start, hands every living crew member a trick-or-treat bag
/// and arms a handful of unsecured closets/lockers as one-shot jump-scare traps.
/// </summary>
[DataDefinition]
public sealed partial class TrickOrTreatCelebrate : IHolidayCelebrate
{
    [DataField]
    public EntProtoId TreatBag = "TrickOrTreatBag";

    [DataField]
    public EntityTableSelector? TrapEffects;

    /// <summary>
    /// Chance that any given unsecured closet/locker gets armed as a jump-scare trap.
    /// </summary>
    [DataField]
    public float TrapChance = 0.12f;

    /// <summary>
    /// Dataset of silly prefixes stuck onto the station's name for the duration of spooky season.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? NamePrefixes = "SpookyStationNamePrefixes";

    /// <summary>
    /// Dataset of silly station announcements read out alongside the rename.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? AnnouncementLines = "SpookyStationAnnouncements";

    public void Celebrate(HolidayPrototype holiday)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var proto = IoCManager.Resolve<IPrototypeManager>();
        var mobState = entMan.System<MobStateSystem>();
        var stationSystem = entMan.System<StationSystem>();
        var chat = entMan.System<Content.Server.Chat.Systems.ChatSystem>();

        var players = entMan.EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out _, out var state, out var xform))
        {
            if (!mobState.IsAlive(uid, state))
                continue;

            entMan.SpawnEntity(TreatBag, xform.Coordinates);
        }

        if (TrapEffects is null)
            return;

        var closets = entMan.EntityQueryEnumerator<EntityStorageComponent>();
        while (closets.MoveNext(out var uid, out _))
        {
            // Leave secure lockers (command, armory, etc.) alone - they're access-gated already.
            if (entMan.HasComponent<AccessReaderComponent>(uid))
                continue;

            if (!random.Prob(TrapChance))
                continue;

            var trap = entMan.EnsureComponent<SpookyClosetTrapComponent>(uid);
            trap.Effects = TrapEffects;
        }

        string? announcement = null;
        if (AnnouncementLines is { } announceSet && proto.Resolve(announceSet, out var announceLines))
            announcement = random.Pick(announceLines);

        if (NamePrefixes is not { } prefixSet || !proto.Resolve(prefixSet, out var prefixes))
            return;

        var prefix = random.Pick(prefixes);
        var stations = entMan.EntityQueryEnumerator<StationDataComponent, MetaDataComponent>();
        while (stations.MoveNext(out var station, out var stationData, out var meta))
        {
            stationSystem.RenameStation(station, $"{prefix} {meta.EntityName}", loud: false, stationData, meta);

            if (announcement is not null)
                chat.DispatchStationAnnouncement(station, announcement, playDefaultSound: true);
        }
    }
}
