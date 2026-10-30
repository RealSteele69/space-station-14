using Content.Server.Antag;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Station.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Antag;
using Content.Shared.GameTicking.Rules;

namespace Content.Server._ClawCommand.AdvancedXenos.Rules;

public sealed partial class AdvXenomorphRuleSystem : GameRuleSystem<AdvXenomorphRuleComponent>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WeldableSystem _weldable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenomorphRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
    }

    protected override void Started(EntityUid uid, AdvXenomorphRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.AnnouncementDelay is { } delay)
            component.AnnounceAt = _timing.CurTime + delay;
    }

    protected override void ActiveTick(EntityUid uid, AdvXenomorphRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.AnnounceAt is not { } at || _timing.CurTime < at)
            return;

        component.AnnounceAt = null;
        _chat.DispatchGlobalAnnouncement(Loc.GetString(component.Announcement), Loc.GetString(component.AnnouncementSender),
            playSound: false, announcementSound: component.AnnouncementSound, colorOverride: Color.Gold);
    }

    private void OnSelectLocation(Entity<AdvXenomorphRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        var big = new List<MapCoordinates>();
        var any = new List<MapCoordinates>();

        var query = EntityQueryEnumerator<GasVentPumpComponent, NodeContainerComponent, TransformComponent>();
        while (query.MoveNext(out var vent, out _, out var nodes, out var xform))
        {
            if (!xform.Anchored || _weldable.IsWelded(vent) || !HasComp<StationMemberComponent>(xform.GridUid))
                continue;

            var coords = _transform.GetMapCoordinates(vent, xform);
            any.Add(coords);

            foreach (var node in nodes.Nodes.Values)
            {
                if (node.NodeGroup is { } group && group.Nodes.Count > ent.Comp.MinNetworkSize)
                {
                    big.Add(coords);
                    break;
                }
            }
        }

        args.Coordinates.AddRange(big.Count > 0 ? big : any);
    }
}
