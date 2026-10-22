using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._ClawCommand.Blob.Components;
using Content.Server._ClawCommand.Blob.Components;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Nuke;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.AlertLevel;
using Content.Shared.GameTicking.Components;
using Content.Shared.Objectives.Components;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Blob.GameTicking;

public sealed partial class BlobRuleSystem : GameRuleSystem<BlobRuleComponent>
{
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private RoundEndSystem _roundEndSystem = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private NukeCodePaperSystem _nukeCode = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EmergencyShuttleSystem _emergency = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlobRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
    }

    private void OnNukeExploded(NukeExplodedEvent ev)
    {
        if (ev.OwningStation is { } grid
            && _stationSystem.GetOwningStation(grid) != null
            && GameTicker.IsGameRuleActive<BlobRuleComponent>())
            _roundEndSystem.EndRound();
    }

    protected override void Started(EntityUid uid, BlobRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var activeRules = QueryActiveRules();
        while (activeRules.MoveNext(out var entityUid, out _, out _, out _))
        {
            if(uid == entityUid)
                continue;

            GameTicker.EndGameRule(uid, gameRule);
            Log.Warning("blob is active!!! remove!");
            break;
        }
    }

    protected override void ActiveTick(EntityUid uid, BlobRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        component.Accumulator += frameTime;

        if(component.Accumulator < 10)
            return;

        component.Accumulator = 0;

        var check = new Dictionary<EntityUid, long>();
        var blobCoreQuery = EntityQueryEnumerator<BlobCoreComponent, MetaDataComponent, TransformComponent>();
        while (blobCoreQuery.MoveNext(out var ent, out var comp, out var md, out var xform))
        {
            if (TerminatingOrDeleted(ent, md) ||
                !CheckBlobInStation(ent, xform, out var stationUid))
            {
                continue;
            }

            check.TryAdd(stationUid.Value, 0);

            check[stationUid.Value] += comp.BlobTiles.Count;
        }

        foreach (var (station, length) in check.AsParallel())
        {
            CheckChangeStage(station, component, length);
        }
    }

    private bool CheckBlobInStation(EntityUid blobCore, TransformComponent? xform, [NotNullWhen(true)] out EntityUid? stationUid)
    {
        var station = _stationSystem.GetOwningStation(blobCore, xform);
        if (station == null || !HasComp<StationEventEligibleComponent>(station.Value))
        {
            _chatManager.SendAdminAlert(blobCore, Loc.GetString("blob-alert-out-off-station"));
            QueueDel(blobCore);
            stationUid = null;
            return false;
        }

        stationUid = station.Value;
        return true;
    }

    private static readonly ProtoId<AlertLevelPrototype> StationAlertCritical = "DeltaBlob";
    private static readonly ProtoId<AlertLevelPrototype> StationAlertDetected = "Red";

    private void CheckChangeStage(
        Entity<StationBlobConfigComponent?> stationUid,
        BlobRuleComponent blobRuleComp,
        long blobTilesCount)
    {
        Resolve(stationUid, ref stationUid.Comp, false);

        var stationName = Name(stationUid);

        var shuttleLock = stationUid.Comp?.ShuttleLock ?? StationBlobConfigComponent.DefaultShuttleLock;
        if (blobTilesCount >= shuttleLock
            && _roundEndSystem.ExpectedCountdownEnd != null
            && !_emergency.EmergencyShuttleArrived)
        {
            _roundEndSystem.CancelRoundEndCountdown(forceRecall: true);
            _chatSystem.DispatchStationAnnouncement(stationUid,
                Loc.GetString("blob-alert-recall-shuttle"),
                Loc.GetString("blob-announcement-sender"),
                false,
                null,
                Color.Red);
        }
        else if (blobTilesCount >= shuttleLock
                 && _roundEndSystem.ExpectedCountdownEnd != null && _emergency.EmergencyShuttleArrived
                 && !blobRuleComp.ShuttleArrivedAnnounced)
        {
            blobRuleComp.ShuttleArrivedAnnounced = true;
            _chatSystem.DispatchStationAnnouncement(stationUid,
                Loc.GetString("blob-alert-shuttle-arrived"),
                Loc.GetString("blob-announcement-sender"),
                false,
                null,
                Color.OrangeRed);
        }

        switch (blobRuleComp.Stage)
        {
            case BlobStage.Default when blobTilesCount >= (stationUid.Comp?.StageBegin ?? StationBlobConfigComponent.DefaultStageBegin):
                blobRuleComp.Stage = BlobStage.Begin;

                _chatSystem.DispatchGlobalAnnouncement(
                    Loc.GetString("blob-alert-detect"),
                    stationName,
                    true,
                    blobRuleComp.DetectedAudio,
                    Color.Red);

                _alertLevelSystem.SetLevel(stationUid.Owner, StationAlertDetected, true, true, true, true);

                RaiseLocalEvent(stationUid,
                    new BlobChangeLevelEvent
                {
                    Station = stationUid,
                    Level = blobRuleComp.Stage
                },
                    broadcast: true);
                return;
            case BlobStage.Begin when blobTilesCount >= (stationUid.Comp?.StageCritical ?? StationBlobConfigComponent.DefaultStageCritical):
            {
                if (_nukeCode.SendNukeCodes(stationUid))//send the nuke code?
                {
                    blobRuleComp.Stage = BlobStage.Critical;
                    _chatSystem.DispatchGlobalAnnouncement(
                    Loc.GetString("blob-alert-critical"),
                    stationName,
                    true,
                    blobRuleComp.CriticalAudio,
                    Color.Red);
                }
                else
                {
                    blobRuleComp.Stage = BlobStage.Critical;
                    _chatSystem.DispatchGlobalAnnouncement(
                    Loc.GetString("blob-alert-critical-NoNukeCode"),
                    stationName,
                    true,
                    blobRuleComp.CriticalAudio,
                    Color.Red);
                }

                _alertLevelSystem.SetLevel(stationUid.Owner, StationAlertCritical, true, true, true, true);

                RaiseLocalEvent(stationUid,
                    new BlobChangeLevelEvent
                {
                    Station = stationUid,
                    Level = blobRuleComp.Stage
                },
                    broadcast: true);
                return;
            }
            case BlobStage.Critical when blobTilesCount >= (stationUid.Comp?.StageTheEnd ?? StationBlobConfigComponent.DefaultStageEnd):
            {
                blobRuleComp.Stage = BlobStage.TheEnd;
                _roundEndSystem.EndRound();

                RaiseLocalEvent(stationUid,
                    new BlobChangeLevelEvent
                {
                    Station = stationUid,
                    Level = blobRuleComp.Stage
                },
                    broadcast: true);
                return;
            }
        }
    }

    protected override void AppendRoundEndText(
        EntityUid uid,
        BlobRuleComponent blob,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent ev)
    {
        if (blob.Blobs.Count < 1)
            return;

        // Objectives are listed by the generic objectives summary, so only say who the blobs were.
        var result = Loc.GetString("blob-round-end-result", ("blobCount", blob.Blobs.Count));

        foreach (var (mindId, mind) in blob.Blobs)
        {
            var name = mind.CharacterName;
            _player.TryGetSessionByEntity(mindId, out var session);
            var username = session?.Name;

            if (username != null)
            {
                result += "\n" + (name == null
                    ? Loc.GetString("blob-user-was-a-blob", ("user", username))
                    : Loc.GetString("blob-user-was-a-blob-named", ("user", username), ("name", name)));
            }
            else if (name != null)
            {
                result += "\n" + Loc.GetString("blob-was-a-blob-named", ("name", name));
            }
        }

        ev.AddLine(result);
    }

    public void MakeBlob(EntityUid player)
    {
        var comp = EnsureComp<BlobCarrierComponent>(player);
        comp.HasMind = HasComp<ActorComponent>(player);
        comp.TransformationDelay = 10 * 60; // 10min
    }

    private void AfterAntagSelected(EntityUid uid, BlobRuleComponent component, AfterAntagEntitySelectedEvent args)
    {
        MakeBlob(args.EntityUid);
    }
}
