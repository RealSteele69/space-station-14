// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Goobstation.Shared.Religion;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Possession;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Bible.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Heretic;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Zombies;
using Robust.Server.Containers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Possession;

/// <summary>
/// Added to a body that is currently possessed. Removing it ends the possession.
/// </summary>
[RegisterComponent]
public sealed partial class PossessedComponent : Component
{
    [ViewVariables]
    public EntityUid? OriginalMindId;

    [ViewVariables]
    public EntityUid OriginalEntity;

    [ViewVariables]
    public EntityUid PossessorMindId;

    [ViewVariables]
    public EntityUid PossessorOriginalEntity;

    /// <summary>
    /// The jaunt the possessor's body was turned into while away, reverted when the possession ends.
    /// </summary>
    [ViewVariables]
    public EntityUid? PossessorJaunt;

    [ViewVariables]
    public TimeSpan PossessionEndTime;

    [ViewVariables]
    public TimeSpan PossessionTimeRemaining;

    [ViewVariables]
    public bool WasPacified;

    [ViewVariables]
    public bool WasWeakToHoly;

    /// <summary>
    /// Holds a dummy for the body's own mind while the possessor is in control.
    /// </summary>
    [ViewVariables]
    public Container PossessedContainer = default!;

    [DataField]
    public EntProtoId EndPossessionAction = "ActionEndPossession";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Whether the possessor's own body was turned into a jaunt for the duration.
    /// </summary>
    [DataField]
    public bool PolymorphEntity = true;

    [DataField]
    public ProtoId<PolymorphPrototype> Polymorph = "ShadowJauntPermanent";

    [DataField]
    public SoundSpecifier PossessionSound = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/crack2.ogg");

    [DataField]
    public EntProtoId MindDummy = "FoodSnackLollypop";
}

public sealed partial class PossessionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private FollowerSystem _follower = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PossessedComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<PossessedComponent, ComponentRemove>(OnComponentRemoved);
        SubscribeLocalEvent<PossessedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PossessedComponent, EndPossessionEarlyEvent>(OnEarlyEnd);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PossessedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.PossessionEndTime)
            {
                RemCompDeferred<PossessedComponent>(uid);
                continue;
            }

            comp.PossessionTimeRemaining = comp.PossessionEndTime - _timing.CurTime;
        }
    }

    private void OnInit(Entity<PossessedComponent> possessed, ref MapInitEvent args)
    {
        if (!HasComp<WeakToHolyComponent>(possessed))
            AddComp<WeakToHolyComponent>(possessed).AlwaysTakeHoly = true;
        else
            possessed.Comp.WasWeakToHoly = true;

        _action.AddAction(possessed, ref possessed.Comp.ActionEntity, possessed.Comp.EndPossessionAction);

        possessed.Comp.PossessedContainer = _container.EnsureContainer<Container>(possessed, "PossessedContainer");
    }

    private void OnEarlyEnd(EntityUid uid, PossessedComponent comp, ref EndPossessionEarlyEvent args)
    {
        if (args.Handled)
            return;

        RemCompDeferred(uid, comp);
        args.Handled = true;
    }

    private void OnComponentRemoved(Entity<PossessedComponent> possessed, ref ComponentRemove args)
    {
        MapCoordinates? coordinates = null;

        _action.RemoveAction(possessed.Owner, possessed.Comp.ActionEntity);

        var original = possessed.Comp.OriginalEntity;
        var possessor = possessed.Comp.PossessorOriginalEntity;

        if (!TerminatingOrDeleted(original))
        {
            if (!possessed.Comp.WasPacified)
                RemComp<PacifiedComponent>(original);

            if (!possessed.Comp.WasWeakToHoly)
                RemComp<WeakToHolyComponent>(original);

            coordinates = _transform.GetMapCoordinates(original);
        }

        // Bring the possessor's body back out of the shadows first, so the mind has somewhere to go.
        if (possessed.Comp.PossessorJaunt is { } jaunt
            && !TerminatingOrDeleted(jaunt)
            && _polymorph.Revert(jaunt) is { } reverted)
            possessor = reverted;

        // Return the possessors mind to their body, and the target to theirs.
        if (!TerminatingOrDeleted(possessed.Comp.PossessorMindId))
            _mind.TransferTo(possessed.Comp.PossessorMindId, possessor);
        if (possessed.Comp.OriginalMindId is { } originalMind && !TerminatingOrDeleted(originalMind))
            _mind.TransferTo(originalMind, original);

        if (!TerminatingOrDeleted(possessor))
        {
            UpdateFollowersToNewEntity(possessed.Owner, possessor);

            // Teleport to the entity, kinda like you're popping out of their head!
            if (coordinates is { } coords)
                _transform.SetMapCoordinates(possessor, coords);
        }

        if (!TerminatingOrDeleted(original))
        {
            // Paralyze, so you can't just magdump them.
            _stun.TryUpdateParalyzeDuration(original, TimeSpan.FromSeconds(10));
            _popup.PopupEntity(Loc.GetString("possession-end-popup", ("target", original)), original, PopupType.LargeCaution);
        }

        if (possessed.Comp.PossessedContainer != null)
            _container.CleanContainer(possessed.Comp.PossessedContainer);
    }

    private void OnExamined(Entity<PossessedComponent> possessed, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || args.Examined != args.Examiner)
            return;

        var timeRemaining = Math.Floor(possessed.Comp.PossessionTimeRemaining.TotalSeconds);
        args.PushMarkup(Loc.GetString("possessed-component-examined", ("timeremaining", timeRemaining)));
    }

    /// <summary>
    /// Attempts to temporarily possess a target.
    /// </summary>
    /// <param name="possessed">The entity being possessed.</param>
    /// <param name="possessor">The entity possessing the previous entity.</param>
    /// <param name="possessionDuration">How long does the possession last.</param>
    /// <param name="pacifyPossessed">Should the possessor be pacified while inside the possessed body?</param>
    /// <param name="doesMindshieldBlock">Does having a mindshield block being possessed?</param>
    /// <param name="doesChaplainBlock">Is the chaplain immune to this possession?</param>
    /// <param name="polymorphPossessor">Should the possessor's own body be turned into a jaunt meanwhile?</param>
    /// <param name="doesImmuneBlock">Does <see cref="PossessionImmuneComponent"/> block it?</param>
    public bool TryPossessTarget(
        EntityUid possessed,
        EntityUid possessor,
        TimeSpan possessionDuration,
        bool pacifyPossessed,
        bool doesMindshieldBlock = false,
        bool doesChaplainBlock = true,
        bool polymorphPossessor = true,
        bool doesImmuneBlock = true)
    {
        string? failure = null;

        // Possessing a dead guy? What.
        if (_mobState.IsIncapacitated(possessed) || HasComp<ZombieComponent>(possessed))
            failure = "possession-fail-target-dead";
        // Can't possess polymorphed entities. Sends you straight to the shadow realm if you do.
        else if (HasComp<PolymorphedEntityComponent>(possessed))
            failure = "possession-fail-target-polymorphed";
        else if (doesImmuneBlock && HasComp<PossessionImmuneComponent>(possessed))
            failure = "possession-fail-target-immune";
        else if (doesMindshieldBlock && HasComp<MindShieldComponent>(possessed))
            failure = "possession-fail-target-shielded";
        else if (doesChaplainBlock && HasComp<BibleUserComponent>(possessed))
            failure = "possession-fail-target-chaplain";
        else if (HasComp<PossessedComponent>(possessed))
            failure = "possession-fail-target-already-possessed";
        else if (HasComp<DevilComponent>(possessed))
            failure = "possession-fail-devil";
        else if (HasComp<GhoulComponent>(possessed))
            failure = "possession-fail-ghoul";
        else if (HasComp<GhostComponent>(possessed) || HasComp<SpectralComponent>(possessed))
            failure = "possession-fail-ghost";
        else if (HasComp<TimedDespawnComponent>(possessed))
            failure = "possession-fail-temporary";

        if (failure != null)
        {
            _popup.PopupEntity(Loc.GetString(failure), possessor, possessor);
            return false;
        }

        if (!_mind.TryGetMind(possessor, out var possessorMind, out _))
            return false;

        DoPossess(possessed, possessor, possessionDuration, possessorMind, pacifyPossessed, polymorphPossessor);
        return true;
    }

    private void DoPossess(EntityUid possessed, EntityUid possessor, TimeSpan possessionDuration, EntityUid possessorMind, bool pacifyPossessed, bool polymorphPossessor)
    {
        var possessedComp = EnsureComp<PossessedComponent>(possessed);

        if (pacifyPossessed)
        {
            if (HasComp<PacifiedComponent>(possessed))
                possessedComp.WasPacified = true;
            else
                EnsureComp<PacifiedComponent>(possessed);
        }

        possessedComp.PolymorphEntity = polymorphPossessor;

        var currentFollowedEntity = possessor;

        // The possessor's body melts into the shadows meanwhile. Reverting the jaunt brings it back.
        if (polymorphPossessor && _polymorph.PolymorphEntity(possessor, possessedComp.Polymorph) is { } polymorphed)
        {
            UpdateFollowersToNewEntity(possessor, polymorphed);
            currentFollowedEntity = polymorphed;
            possessedComp.PossessorJaunt = polymorphed;
        }

        possessedComp.PossessionEndTime = _timing.CurTime + possessionDuration;

        possessedComp.PossessorOriginalEntity = possessor;
        possessedComp.PossessorMindId = possessorMind;
        possessedComp.OriginalEntity = possessed;

        if (_mind.TryGetMind(possessed, out var possessedMind, out _))
        {
            possessedComp.OriginalMindId = possessedMind;

            // Nobodies gonna know.
            var dummy = Spawn(possessedComp.MindDummy, MapCoordinates.Nullspace);
            _container.Insert(dummy, possessedComp.PossessedContainer);
            _mind.TransferTo(possessedMind, dummy);
        }

        // Transfer into target
        _mind.TransferTo(possessorMind, possessed);

        // After the mind transfer, ghosts should follow the possessed entity (where the mind now is)
        if (!TerminatingOrDeleted(currentFollowedEntity))
            UpdateFollowersToNewEntity(currentFollowedEntity, possessed);

        _popup.PopupEntity(Loc.GetString("possession-popup-self"), possessed, possessed, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("possession-popup-others", ("target", possessed)), possessed, PopupType.MediumCaution);
        _audio.PlayPvs(possessedComp.PossessionSound, possessed);

        _admin.Add(LogType.Mind, LogImpact.High, $"{ToPrettyString(possessor)} possessed {ToPrettyString(possessed)}");
    }

    private void UpdateFollowersToNewEntity(EntityUid oldEntity, EntityUid newEntity)
    {
        if (!TryComp<FollowedComponent>(oldEntity, out var followed))
            return;

        foreach (var follower in new List<EntityUid>(followed.Following))
        {
            if (HasComp<GhostComponent>(follower))
                _follower.StartFollowingEntity(follower, newEntity);
        }
    }
}
