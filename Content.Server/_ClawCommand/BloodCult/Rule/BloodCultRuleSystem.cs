using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._ClawCommand.BloodCult.Items;
using Content.Server._ClawCommand.BloodCult.Objectives;
using Content.Server._ClawCommand.BloodCult.Runes;
using Content.Server._ClawCommand.BloodCult.Spells;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Pinpointer;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared._ClawCommand.BloodCult.Roles;
using Content.Shared._ClawCommand.BloodCult.Systems;
using Content.Shared._ClawCommand.Mood;
using Content.Shared.Body;
using Content.Shared.Actions;
using Content.Shared.Cloning.Events;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Gibbing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.BloodCult.Rule;

public sealed partial class BloodCultRuleSystem : GameRuleSystem<BloodCultRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private BloodSpearSystem _bloodSpear = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMoodSystem _mood = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    private const string CultFocusedMood = "CultFocused";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<BloodCultNarsieSummonedEvent>(OnNarsieSummoned);

        SubscribeLocalEvent<BloodCultistComponent, ComponentRemove>(OnCultistRemoved);
        SubscribeLocalEvent<BloodCultistComponent, MobStateChangedEvent>(OnCultistStateChanged);
        SubscribeLocalEvent<BloodCultistComponent, CloningEvent>(OnCultistCloned);

        SubscribeLocalEvent<BloodCultRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    #region Rule lifecycle

    protected override void Started(EntityUid uid, BloodCultRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        PickRendingPlacements(component);
    }

    protected override void AppendRoundEndText(EntityUid uid,
        BloodCultRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        args.AddLine(Loc.GetString($"blood-cult-condition-{component.WinCondition.ToString().ToLowerInvariant()}"));
        args.AddLine(Loc.GetString("blood-cult-summary-converted", ("count", component.TotalConverted)));
        args.AddLine(Loc.GetString("blood-cult-summary-sacrificed", ("count", component.TotalSacrificed)));

        if (component.OfferingTarget is { } target && !TerminatingOrDeleted(target))
        {
            var sacrificed = !HasComp<MobStateComponent>(target) || _mobState.IsDead(target);
            args.AddLine(Loc.GetString(sacrificed ? "blood-cult-summary-target-dead" : "blood-cult-summary-target-alive",
                ("name", Name(target))));
        }

        args.AddLine(Loc.GetString("blood-cultists-list-start"));

        // Roundstart cultists through the antag selection, everyone converted later from the roster.
        var listed = new HashSet<string>();
        foreach (var (_, data, name) in _antag.GetAntagIdentifiers(uid))
        {
            listed.Add(name);
            args.AddLine(Loc.GetString("blood-cultists-list-name", ("name", name), ("user", data.UserName)));
        }

        foreach (var (_, name) in component.Roster)
        {
            if (listed.Add(name))
                args.AddLine(Loc.GetString("blood-cultists-list-name-converted", ("name", name)));
        }
    }

    #endregion

    #region Event handlers

    private void OnAntagSelected(Entity<BloodCultRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeCultist(args.EntityUid, ent, false);
    }

    private void OnNarsieSummoned(BloodCultNarsieSummonedEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            rule.WinCondition = BloodCultWinCondition.Win;

            foreach (var cultist in rule.Cultists.ToList())
            {
                if (TerminatingOrDeleted(cultist) || !_mind.TryGetMind(cultist, out var mindId, out _))
                    continue;

                var harvester = Spawn(rule.HarvesterPrototype, Transform(cultist).Coordinates);
                _mind.TransferTo(mindId, harvester);
                _gibbing.Gib(cultist);
            }
        }

        _roundEnd.EndRound(TimeSpan.FromMinutes(1));
    }

    private void OnCultistRemoved(Entity<BloodCultistComponent> cultist, ref ComponentRemove args)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            rule.Cultists.Remove(cultist);
            if (rule.CultLeader == cultist.Owner)
            {
                rule.CultLeader = null;
                rule.LeaderSelected = false;
            }
        }

        CheckWinCondition();

        if (TerminatingOrDeleted(cultist))
            return;

        RemoveCultItems(cultist);
        RemoveCultistAppearance(cultist);
        RemoveObjectiveAndRole(cultist);
        RemComp<BloodCultLeaderComponent>(cultist);
        _mood.RemoveMoodlet(cultist, CultFocusedMood);

        query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            _faction.RemoveFaction(cultist.Owner, rule.BloodCultFaction);
            _faction.AddFaction(cultist.Owner, rule.NanoTrasenFaction);

            if (TryComp<IntrinsicRadioTransmitterComponent>(cultist, out var transmitter))
                transmitter.Channels.Remove(rule.CultChannel);
            if (TryComp<ActiveRadioComponent>(cultist, out var radio))
                radio.Channels.Remove(rule.CultChannel);

            _antag.SendBriefing(cultist, Loc.GetString("blood-cult-deconverted"), Color.LightBlue, rule.DeconvertSound);
            break;
        }

        if (TryComp<BloodCultSpellsHolderComponent>(cultist, out var spells))
        {
            foreach (var spell in spells.SelectedSpells)
                _actions.RemoveAction(cultist.Owner, spell);

            _actions.RemoveAction(cultist.Owner, spells.PrepareAction);
            RemComp(cultist, spells);
        }

        RemComp<BloodCultEmpoweredComponent>(cultist);
        RemComp<BloodCultHaloComponent>(cultist);
    }

    private void OnCultistStateChanged(Entity<BloodCultistComponent> cultist, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            CheckWinCondition();
    }

    private void OnCultistCloned(Entity<BloodCultistComponent> cultist, ref CloningEvent args)
    {
        // The clone is a new body without the blood pact.
        RemComp<BloodCultistComponent>(args.CloneUid);
    }

    private void OnGetBriefing(Entity<BloodCultRoleComponent> role, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("blood-cult-role-briefing-short"));

        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (rule.OfferingTarget is { } target && !TerminatingOrDeleted(target))
                args.Append(Loc.GetString("blood-cult-role-briefing-target", ("name", Name(target))));

            if (rule.EmergencyMarkersMode)
            {
                args.Append(Loc.GetString("blood-cult-role-briefing-emergency-rending", ("amount", rule.EmergencyMarkersCount)));
                return;
            }

            break;
        }

        var query = EntityQueryEnumerator<RendingRunePlacementMarkerComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (!marker.IsActive)
                continue;

            var location = FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(uid));
            args.Append(Loc.GetString("blood-cult-role-briefing-rending-locations", ("location", location)));
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Converts a living, minded entity into a cultist, joining the first active cult.
    /// </summary>
    public bool Convert(EntityUid target, EntityUid? converter = null)
    {
        if (HasComp<BloodCultistComponent>(target))
            return false;

        var query = QueryActiveRules();
        while (query.MoveNext(out var ruleUid, out _, out var rule, out _))
        {
            if (!MakeCultist(target, (ruleUid, rule), true))
                return false;

            rule.TotalConverted++;
            if (converter != null)
                _adminLog.Add(LogType.Mind, LogImpact.High, $"{ToPrettyString(converter.Value):player} converted {ToPrettyString(target):player} into a blood cultist");

            return true;
        }

        return false;
    }

    /// <summary>
    /// Counts a sacrifice towards the round summary.
    /// </summary>
    public void RecordSacrifice()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            rule.TotalSacrificed++;
        }
    }

    /// <summary>
    /// The sacrifice is done once the target is dead, gone or no longer a living mob.
    /// </summary>
    public bool IsObjectiveFinished()
    {
        if (!TryGetTarget(out var target))
            return true;

        return TerminatingOrDeleted(target.Value) || !HasComp<MobStateComponent>(target) || _mobState.IsDead(target.Value);
    }

    public bool TryGetTarget([NotNullWhen(true)] out EntityUid? target)
    {
        target = null;
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            if (rule.OfferingTarget is not { } offering)
                continue;

            target = offering;
            return true;
        }

        return false;
    }

    public bool IsTarget(EntityUid uid)
    {
        return TryGetTarget(out var target) && target == uid;
    }

    /// <summary>
    /// Living cultists plus cult constructs.
    /// </summary>
    public int GetTotalCultists()
    {
        var constructs = 0;
        var constructQuery = EntityQueryEnumerator<BloodCultConstructComponent>();
        while (constructQuery.MoveNext(out _, out var construct))
        {
            if (!construct.Holy)
                constructs++;
        }

        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            return rule.Cultists.Count + constructs;
        }

        return 0;
    }

    /// <summary>
    /// Picks the sacrifice target if the cult doesn't have one yet.
    /// </summary>
    public EntityUid? EnsureOfferingTarget()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            if (rule.OfferingTarget == null || TerminatingOrDeleted(rule.OfferingTarget.Value))
                SetRandomCultTarget(rule);

            return rule.OfferingTarget;
        }

        return null;
    }

    /// <summary>
    /// Whether the final runes may be drawn where <paramref name="user"/> stands.
    /// </summary>
    public bool CanDrawRendingRune(EntityUid user)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (rule is { EmergencyMarkersMode: true, EmergencyMarkersCount: > 0 })
                return true;
        }

        var userCoords = Transform(user).Coordinates;
        var query = EntityQueryEnumerator<RendingRunePlacementMarkerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var marker, out var xform))
        {
            if (marker.IsActive && _transform.InRange(xform.Coordinates, userCoords, marker.DrawingRange))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Uses up the place where a final rune was just drawn.
    /// </summary>
    public void ConsumeNearestMarker(EntityUid user)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (rule is not { EmergencyMarkersMode: true, EmergencyMarkersCount: > 0 })
                continue;

            rule.EmergencyMarkersCount--;
            return;
        }

        var userCoords = Transform(user).Coordinates;
        var query = EntityQueryEnumerator<RendingRunePlacementMarkerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var marker, out var xform))
        {
            if (!marker.IsActive || !_transform.InRange(xform.Coordinates, userCoords, marker.DrawingRange))
                continue;

            marker.IsActive = false;
            return;
        }
    }

    public void RemoveObjectiveAndRole(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        for (var i = mind.Objectives.Count - 1; i >= 0; i--)
        {
            var objective = mind.Objectives[i];
            if (HasComp<KillTargetCultComponent>(objective) || HasComp<SummonNarsieConditionComponent>(objective))
                _mind.TryRemoveObjective(mindId, mind, i);
        }

        _role.MindRemoveRole<BloodCultRoleComponent>(mindId);
    }

    #endregion

    #region Helpers

    private bool MakeCultist(EntityUid uid, Entity<BloodCultRuleComponent> rule, bool converted)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return false;

        EnsureComp<BloodCultistComponent>(uid);
        rule.Comp.Cultists.Add(uid);
        rule.Comp.Roster[uid] = Name(uid);

        _faction.RemoveFaction(uid, rule.Comp.NanoTrasenFaction);
        _faction.AddFaction(uid, rule.Comp.BloodCultFaction);

        // The cult speaks to itself over its own channel (:g).
        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        EnsureComp<IntrinsicRadioTransmitterComponent>(uid).Channels.Add(rule.Comp.CultChannel);
        EnsureComp<ActiveRadioComponent>(uid).Channels.Add(rule.Comp.CultChannel);

        var spells = EnsureComp<BloodCultSpellsHolderComponent>(uid);
        if (spells.PrepareAction == null)
            spells.PrepareAction = _actions.AddAction(uid, rule.Comp.SpellsAction);

        _ui.SetUi(uid, BloodCultUiKey.Spells, new InterfaceData("BloodCultRadialBoundUserInterface", -1f));
        _ui.SetUi(uid, BloodCultUiKey.TeleportSpell, new InterfaceData("BloodCultListBoundUserInterface", -1f));

        _mood.AddMoodlet(uid, CultFocusedMood);

        if (!_role.MindHasRole<BloodCultRoleComponent>(mindId))
            _role.MindAddRole(mindId, rule.Comp.MindRole, mind, true);

        EnsureOfferingTarget();
        _mind.TryAddObjective(mindId, mind, rule.Comp.Objective);
        _mind.TryAddObjective(mindId, mind, rule.Comp.SummonObjective);

        if (converted)
        {
            _antag.SendBriefing(uid, Loc.GetString("blood-cult-role-converted"), Color.DarkRed, rule.Comp.ConvertSound);
            _antag.SendBriefing(uid, Loc.GetString("blood-cult-role-briefing-short"), Color.Red, null);
        }

        UpdateCultStage(rule.Comp);

        // Late converts catch up with whatever the cult already shows.
        if (rule.Comp.Stage >= BloodCultStage.RedEyes)
            ApplyRedEyes(uid, rule.Comp);
        if (rule.Comp.Stage >= BloodCultStage.Halos)
            EnsureComp<BloodCultHaloComponent>(uid);

        return true;
    }

    private void CheckWinCondition()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _))
        {
            if (rule.WinCondition == BloodCultWinCondition.Win)
                continue;

            var alive = rule.Cultists.Count(c => !TerminatingOrDeleted(c) && !_mobState.IsDead(c));
            rule.WinCondition = alive == 0 ? BloodCultWinCondition.Failure : BloodCultWinCondition.Draw;
        }
    }

    private void SetRandomCultTarget(BloodCultRuleComponent rule)
    {
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<MindContainerComponent, HumanoidProfileComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _))
        {
            if (HasComp<BloodCultistComponent>(uid) || _mobState.IsDead(uid))
                continue;

            candidates.Add(uid);
        }

        rule.OfferingTarget = candidates.Count > 0 ? _random.Pick(candidates) : null;
    }

    private void PickRendingPlacements(BloodCultRuleComponent rule)
    {
        var markers = new List<RendingRunePlacementMarkerComponent>();
        var query = EntityQueryEnumerator<RendingRunePlacementMarkerComponent>();
        while (query.MoveNext(out _, out var marker))
        {
            markers.Add(marker);
        }

        if (markers.Count == 0)
        {
            rule.EmergencyMarkersMode = true;
            rule.EmergencyMarkersCount = rule.RendingRunePlacementsAmount;
            return;
        }

        for (var i = Math.Min(rule.RendingRunePlacementsAmount, markers.Count); i > 0; i--)
        {
            _random.PickAndTake(markers).IsActive = true;
        }
    }

    private void RemoveCultItems(Entity<BloodCultistComponent> cultist)
    {
        _bloodSpear.DetachSpearFromMaster(cultist);

        if (_inventory.TryGetContainerSlotEnumerator(cultist.Owner, out var enumerator))
        {
            while (enumerator.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } item && TryComp<BloodCultItemComponent>(item, out var cultItem) && !cultItem.AllowUseToEveryone)
                    _container.Remove(item, slot, true, true);
            }
        }

        foreach (var item in _hands.EnumerateHeld(cultist.Owner).ToList())
        {
            if (TryComp<BloodCultItemComponent>(item, out var cultItem) && !cultItem.AllowUseToEveryone &&
                !_hands.TryDrop(cultist.Owner, item, checkActionBlocker: false, doDropInteraction: false))
                QueueDel(item);
        }
    }

    private void ApplyRedEyes(EntityUid uid, BloodCultRuleComponent rule)
    {
        if (!TryComp<BloodCultistComponent>(uid, out var cultist) || cultist.OriginalProfiles != null ||
            !_visualBody.TryGatherMarkingsData(uid, null, out var profiles, out _, out _))
            return;

        cultist.OriginalProfiles = profiles;
        var redEyes = profiles.ToDictionary(pair => pair.Key, pair => pair.Value with { EyeColor = rule.EyeColor });
        _visualBody.ApplyProfiles(uid, redEyes);
    }

    private void RemoveCultistAppearance(Entity<BloodCultistComponent> cultist)
    {
        if (cultist.Comp.OriginalProfiles is { } profiles)
        {
            _visualBody.ApplyProfiles(cultist, profiles);
            cultist.Comp.OriginalProfiles = null;
        }

        RemComp<BloodCultHaloComponent>(cultist);
    }

    private void UpdateCultStage(BloodCultRuleComponent rule)
    {
        var count = rule.Cultists.Count;
        var previous = rule.Stage;

        if (count >= rule.HaloThreshold)
            rule.Stage = BloodCultStage.Halos;
        else if (count >= rule.RedEyesThreshold)
            rule.Stage = BloodCultStage.RedEyes;

        // The cult never hides again once it has revealed itself.
        if (rule.Stage <= previous)
            return;

        foreach (var cultist in rule.Cultists)
        {
            if (rule.Stage >= BloodCultStage.RedEyes && previous < BloodCultStage.RedEyes)
            {
                ApplyRedEyes(cultist, rule);
                _antag.SendBriefing(cultist, Loc.GetString("blood-cult-stage-red-eyes"), Color.DarkRed, rule.RedEyesSound);
            }

            if (rule.Stage >= BloodCultStage.Halos && previous < BloodCultStage.Halos)
            {
                EnsureComp<BloodCultHaloComponent>(cultist);
                _antag.SendBriefing(cultist, Loc.GetString("blood-cult-stage-halos"), Color.DarkRed, rule.HaloSound);
            }
        }

        if (rule.Stage == BloodCultStage.Halos)
            SelectRandomLeader(rule);
    }

    private void SelectRandomLeader(BloodCultRuleComponent rule)
    {
        if (rule.LeaderSelected)
            return;

        var candidates = rule.Cultists.Where(uid =>
                !TerminatingOrDeleted(uid) &&
                !_mobState.IsDead(uid) &&
                !(TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled) &&
                !(TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0))
            .ToList();

        if (candidates.Count == 0)
            return;

        var leader = _random.Pick(candidates);
        EnsureComp<BloodCultLeaderComponent>(leader);
        rule.LeaderSelected = true;
        rule.CultLeader = leader;

        foreach (var cultist in rule.Cultists)
        {
            _antag.SendBriefing(cultist, Loc.GetString("blood-cult-leader-chosen", ("name", Name(leader))), Color.DarkRed, null);
        }
    }

    #endregion
}
