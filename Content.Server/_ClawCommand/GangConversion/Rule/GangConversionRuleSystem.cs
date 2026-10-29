using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Decals;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Shared._ClawCommand.GangConversion;
using Content.Shared._ClawCommand.GangConversion.Components;
using Content.Shared._ClawCommand.GangConversion.Roles;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mindshield;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.GangConversion.Rule;

public sealed partial class GangConversionRuleSystem : GameRuleSystem<GangConversionRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MindShieldSystem _mindShield = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedRoleSystem _role = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GangConversionRuleComponent, AfterAntagEntitySelectedEvent>(OnBossSelected);

        SubscribeLocalEvent<GangConverterComponent, MeleeHitEvent>(OnConverterHit);

        SubscribeLocalEvent<GangMemberComponent, ComponentRemove>(OnMemberRemoved);
        SubscribeLocalEvent<GangMemberComponent, GetVerbsEvent<Verb>>(AddPromoteVerb);

        SubscribeLocalEvent<GangBossRoleComponent, GetBriefingEvent>(OnGetBossBriefing);
        SubscribeLocalEvent<GangMemberRoleComponent, GetBriefingEvent>(OnGetMemberBriefing);
    }

    #region Rule lifecycle

    protected override void ActiveTick(EntityUid uid, GangConversionRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.NextTurfTick > _timing.CurTime)
            return;

        component.NextTurfTick = _timing.CurTime + component.TurfTickInterval;
        RecountTurf(component);
    }

    protected override void AppendRoundEndText(EntityUid uid,
        GangConversionRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (component.Teams.Count == 0)
            return;

        RecountTurf(component);

        var ranked = component.Teams.OrderByDescending(t => t.TurfScore)
            .ThenByDescending(t => t.Members.Count(m => !TerminatingOrDeleted(m) && !_mobState.IsDead(m)))
            .ToList();

        var topScore = ranked[0].TurfScore;
        var winners = ranked.Where(t => t.TurfScore == topScore).ToList();

        args.AddLine(Loc.GetString(winners.Count == 1 && topScore > 0
            ? "gang-condition-winner"
            : "gang-condition-draw",
            ("name", winners[0].Name)));

        foreach (var team in ranked)
        {
            var alive = team.Members.Count(m => !TerminatingOrDeleted(m) && !_mobState.IsDead(m));
            args.AddLine(Loc.GetString("gang-summary-team",
                ("name", team.Name),
                ("boss", team.BossName),
                ("turf", team.TurfScore),
                ("alive", alive),
                ("total", team.Roster.Count)));
        }
    }

    #endregion

    #region Event handlers

    private void OnBossSelected(Entity<GangConversionRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var rule = ent.Comp;
        var id = rule.Teams.Count;
        var team = new GangTeamData
        {
            Id = id,
            Name = PickGangName(rule),
            Color = rule.TeamColors[id % rule.TeamColors.Count],
            Channel = rule.Channels[id % rule.Channels.Count],
        };
        rule.Teams.Add(team);

        MakeGangMember(args.EntityUid, rule, team, isBoss: true, converted: false);
    }

    private void OnConverterHit(Entity<GangConverterComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.GangId < 0 || !args.IsHit)
            return;

        if (!FindRuleWithTeam(ent.Comp.GangId, out var rule, out var team) || rule == null || team == null)
            return;

        foreach (var target in args.HitEntities)
        {
            TryConvert(target, rule, team, args.User);
        }
    }

    private void OnMemberRemoved(Entity<GangMemberComponent> member, ref ComponentRemove args)
    {
        if (!FindRuleWithTeam(member.Comp.GangId, out var rule, out var team) || rule == null || team == null)
            return;

        team.Members.Remove(member.Owner);
        if (team.Boss == member.Owner)
            team.Boss = null;

        if (TerminatingOrDeleted(member.Owner))
            return;

        RemComp<GangBossComponent>(member.Owner);
        RemComp<GangEnforcerComponent>(member.Owner);

        if (TryComp<IntrinsicRadioTransmitterComponent>(member.Owner, out var transmitter))
            transmitter.Channels.Remove(team.Channel);
        if (TryComp<ActiveRadioComponent>(member.Owner, out var radio))
            radio.Channels.Remove(team.Channel);

        if (_mind.TryGetMind(member.Owner, out var mindId, out _))
        {
            if (_role.MindHasRole<GangBossRoleComponent>(mindId))
                _role.MindRemoveRole<GangBossRoleComponent>(mindId);
            if (_role.MindHasRole<GangMemberRoleComponent>(mindId))
                _role.MindRemoveRole<GangMemberRoleComponent>(mindId);
        }

        _antag.SendBriefing(member.Owner, Loc.GetString("gang-deconverted"), Color.LightGray, rule.DeconvertSound);
    }

    private void AddPromoteVerb(Entity<GangMemberComponent> target, ref GetVerbsEvent<Verb> args)
    {
        if (args.User == args.Target || !args.CanInteract || !args.CanAccess)
            return;

        if (!TryComp<GangMemberComponent>(args.User, out var userMember) || !HasComp<GangBossComponent>(args.User))
            return;

        if (userMember.GangId != target.Comp.GangId || HasComp<GangBossComponent>(target.Owner) || HasComp<GangEnforcerComponent>(target.Owner))
            return;

        if (!FindRuleWithTeam(target.Comp.GangId, out var rule, out var team) || rule == null || team == null)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("gang-promote-verb"),
            Category = VerbCategory.Antag,
            Act = () => PromoteToEnforcer(target.Owner, rule, team),
            Impact = LogImpact.Medium,
        });
    }

    private void OnGetBossBriefing(Entity<GangBossRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;
        if (ent is { } uid && FindTeamOf(uid, out var team) && team != null)
            args.Append(Loc.GetString("gang-role-briefing-short-boss", ("gang", team.Name)));
    }

    private void OnGetMemberBriefing(Entity<GangMemberRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;
        if (ent is { } uid && FindTeamOf(uid, out var team) && team != null)
            args.Append(Loc.GetString("gang-role-briefing-short-member", ("gang", team.Name)));
    }

    #endregion

    #region Public API

    /// <summary>
    /// Read-only snapshot of a gang round's teams, for admin tooling and tests. The component itself is
    /// locked to this system, so this is the supported way to look at gang state from outside it.
    /// </summary>
    public IReadOnlyList<GangTeamData> GetTeams(Entity<GangConversionRuleComponent> rule) => rule.Comp.Teams;

    public string GetTagDecalId(Entity<GangConversionRuleComponent> rule) => rule.Comp.TagDecalId;

    /// <summary>
    /// Forces an immediate turf recount instead of waiting for the next <see cref="ActiveTick"/>.
    /// </summary>
    public void ForceTurfRecount(Entity<GangConversionRuleComponent> rule) => RecountTurf(rule.Comp);

    #endregion

    #region Helpers

    private bool TryConvert(EntityUid target, GangConversionRuleComponent rule, GangTeamData team, EntityUid? converter)
    {
        if (TryComp<GangMemberComponent>(target, out var existing) && existing.GangId == team.Id)
            return false; // already one of ours

        if (!HasComp<HumanoidProfileComponent>(target) || !_mobState.IsAlive(target))
            return false;

        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        var attempt = new AttemptConvertGangEvent();
        RaiseLocalEvent(target, ref attempt);

        _mindShield.GetMindshieldStatus(target, out var isMindshielded, out _);
        if (attempt.Cancelled || isMindshielded)
            return false;

        // Defecting from another gang: leave the old one first, losing any rank.
        if (existing != null && FindRuleWithTeam(existing.GangId, out _, out var oldTeam) && oldTeam != null && oldTeam != team)
        {
            RemComp<GangBossComponent>(target);
            RemComp<GangEnforcerComponent>(target);

            oldTeam.Members.Remove(target);
            if (oldTeam.Boss == target)
                oldTeam.Boss = null;

            if (TryComp<IntrinsicRadioTransmitterComponent>(target, out var oldTransmitter))
                oldTransmitter.Channels.Remove(oldTeam.Channel);
            if (TryComp<ActiveRadioComponent>(target, out var oldActiveRadio))
                oldActiveRadio.Channels.Remove(oldTeam.Channel);
        }

        MakeGangMember(target, rule, team, isBoss: false, converted: true);

        if (converter != null)
            _adminLog.Add(LogType.MindRole, LogImpact.High, $"{ToPrettyString(converter.Value):player} converted {ToPrettyString(target):player} into gang '{team.Name}'");

        return true;
    }

    private void MakeGangMember(EntityUid uid, GangConversionRuleComponent rule, GangTeamData team, bool isBoss, bool converted)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        var member = EnsureComp<GangMemberComponent>(uid);
        member.GangId = team.Id;
        member.GangColor = team.Color;
        Dirty(uid, member);

        team.Members.Add(uid);
        team.Roster[uid] = Name(uid);
        team.PeakMembers = Math.Max(team.PeakMembers, team.Members.Count);

        if (isBoss)
        {
            EnsureComp<GangBossComponent>(uid);
            team.Boss = uid;
            team.BossName = Name(uid);
        }

        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        EnsureComp<IntrinsicRadioTransmitterComponent>(uid).Channels.Add(team.Channel);
        EnsureComp<ActiveRadioComponent>(uid).Channels.Add(team.Channel);

        if (isBoss)
        {
            GiveClaimedItem(uid, rule.ConverterItem, team);
            GiveClaimedItem(uid, rule.TaggerItem, team);

            if (!_role.MindHasRole<GangBossRoleComponent>(mindId))
                _role.MindAddRole(mindId, rule.BossMindRole, mind, true);
        }
        else
        {
            if (converted)
                GiveClaimedItem(uid, rule.TaggerItem, team);

            if (!_role.MindHasRole<GangMemberRoleComponent>(mindId))
                _role.MindAddRole(mindId, rule.MemberMindRole, mind, true);
        }

        if (converted)
        {
            _antag.SendBriefing(uid,
                Loc.GetString(isBoss ? "gang-role-greeting-boss" : "gang-role-converted", ("gang", team.Name)),
                team.Color,
                rule.ConvertSound);
        }
    }

    private void PromoteToEnforcer(EntityUid uid, GangConversionRuleComponent rule, GangTeamData team)
    {
        if (!team.Members.Contains(uid) || HasComp<GangEnforcerComponent>(uid) || HasComp<GangBossComponent>(uid))
            return;

        EnsureComp<GangEnforcerComponent>(uid);
        GiveClaimedItem(uid, rule.ConverterItem, team);

        _antag.SendBriefing(uid, Loc.GetString("gang-promote-success", ("gang", team.Name)), team.Color, null);
    }

    private void GiveClaimedItem(EntityUid uid, EntProtoId proto, GangTeamData team)
    {
        var item = Spawn(proto, Transform(uid).Coordinates);

        if (TryComp<GangConverterComponent>(item, out var converter))
            converter.GangId = team.Id;

        if (TryComp<GangTaggerComponent>(item, out var tagger))
        {
            tagger.GangId = team.Id;
            tagger.Color = team.Color;
        }

        _hands.TryPickupAnyHand(uid, item);
    }

    private bool FindRuleWithTeam(int gangId, out GangConversionRuleComponent? rule, out GangTeamData? team)
    {
        var query = EntityQueryEnumerator<GangConversionRuleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            foreach (var candidate in comp.Teams)
            {
                if (candidate.Id != gangId)
                    continue;

                rule = comp;
                team = candidate;
                return true;
            }
        }

        rule = null;
        team = null;
        return false;
    }

    private bool FindTeamOf(EntityUid uid, out GangTeamData? team)
    {
        if (TryComp<GangMemberComponent>(uid, out var member) && FindRuleWithTeam(member.GangId, out _, out team))
            return team != null;

        team = null;
        return false;
    }

    private string PickGangName(GangConversionRuleComponent rule)
    {
        var names = _proto.Index(rule.NameDataset).Values.ToList();
        var used = rule.Teams.Select(t => t.Name).ToHashSet();
        names.RemoveAll(used.Contains);

        return names.Count > 0 ? _random.Pick(names) : $"Gang {rule.Teams.Count + 1}";
    }

    private void RecountTurf(GangConversionRuleComponent rule)
    {
        if (rule.Teams.Count == 0)
            return;

        foreach (var team in rule.Teams)
            team.TurfScore = 0;

        var hugeBox = new Box2(new Vector2(-100000, -100000), new Vector2(100000, 100000));
        var gridQuery = EntityQueryEnumerator<MapGridComponent>();
        while (gridQuery.MoveNext(out var gridUid, out _))
        {
            foreach (var (_, decal) in _decals.GetDecalsIntersecting(gridUid, hugeBox))
            {
                if (decal.Id != rule.TagDecalId || decal.Color is not { } color)
                    continue;

                foreach (var team in rule.Teams)
                {
                    if (!ColorsMatch(color, team.Color))
                        continue;

                    team.TurfScore++;
                    break;
                }
            }
        }
    }

    private static bool ColorsMatch(Color a, Color b)
    {
        const float tolerance = 0.01f;
        return MathF.Abs(a.R - b.R) < tolerance &&
               MathF.Abs(a.G - b.G) < tolerance &&
               MathF.Abs(a.B - b.B) < tolerance;
    }

    #endregion
}
