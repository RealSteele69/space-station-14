// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: this fork already has a generic PossessionSystem (Content.Server._ClawCommand.Possession),
// but it's built for temporarily body-jacking a LIVING mob (it hard-requires !IsIncapacitated) and returns
// control automatically after a timer. Wraith needs the opposite semantics - permanently taking over a
// freshly-dead corpse (MakeRevenant) or a temporarily-animated inanimate OBJECT (PossessObject), neither of
// which the generic system supports. Kept as its own small system, same as source, rather than forcing a
// bad fit onto the generic one.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Revenant;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.Magic.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._GoobStation.Wraith.Systems;

/// <summary>
/// This handles getting possessed by the Wraith.
/// This system is hardcoded for Wraith - don't reuse it elsewhere, use the generic PossessionSystem instead.
/// </summary>
public sealed partial class WraithPossessedSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private WraithRevenantSystem _wraithRevenant = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WraithPossessedComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<WraithPossessedComponent, DestructionAttemptEvent>(OnDestructionAttempt);
        SubscribeLocalEvent<WraithPossessedComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var eqe = EntityQueryEnumerator<WraithPossessedComponent>();
        while (eqe.MoveNext(out var uid, out var comp))
        {
            if (comp.CancelEarly)
                ReturnBack((uid, comp));

            if (_timing.CurTime < comp.NextUpdate)
                continue;

            if (HasComp<AnimateableComponent>(uid))
                ReturnBack((uid, comp));
        }
    }

    private void OnMapInit(Entity<WraithPossessedComponent> ent, ref MapInitEvent args)
    {
        if (!_mind.TryGetMind(ent.Owner, out var mindId, out _))
            return;

        ent.Comp.OriginalMind = mindId;
        Dirty(ent);
    }

    private void OnDestructionAttempt(Entity<WraithPossessedComponent> ent, ref DestructionAttemptEvent args)
    {
        // early return on destruction
        if (ent.Comp.PossessorMind == null
            || ent.Comp.Possessor == null)
            return;

        ent.Comp.CancelEarly = true;
        Dirty(ent);

        _mind.TransferTo(ent.Comp.PossessorMind.Value, ent.Comp.Possessor);

        var ev = new PossessionEndedEvent();
        RaiseLocalEvent(ent.Comp.Possessor.Value, ref ev);
    }

    private void OnMobStateChanged(Entity<WraithPossessedComponent> ent, ref MobStateChangedEvent args)
    {
        // early return on death/crit
        if (args.NewMobState == MobState.Alive
            || ent.Comp.PossessorMind == null
            || ent.Comp.Possessor == null)
            return;

        ent.Comp.CancelEarly = true;
        Dirty(ent);

        _mind.TransferTo(ent.Comp.PossessorMind.Value, ent.Comp.Possessor);

        var ev = new PossessionEndedEvent();
        RaiseLocalEvent(ent.Comp.Possessor.Value, ref ev);
    }

    #region Helpers
    /// <summary>
    /// Starts the possession.
    /// </summary>
    /// <param name="ent">The entity that is being possessed</param>
    /// <param name="possessor">The possessor</param>
    /// <param name="possessorMind">The possessor's mind</param>
    /// <param name="makeRev">Whether to make the user into a Revenant</param>
    public void StartPossession(Entity<WraithPossessedComponent> ent,
        EntityUid possessor,
        EntityUid possessorMind,
        bool makeRev = false)
    {
        SetPossessorAndMind(ent, possessor, possessorMind);

        var ev = new PossessionStartedEvent();
        RaiseLocalEvent(possessor, ref ev);

        if (makeRev)
        {
            _mind.TransferTo(possessorMind, ent.Owner);
            var rev = EnsureComp<WraithRevenantComponent>(ent.Owner);

            var alive = new List<MobState> { MobState.Alive };

            _wraithRevenant.SetPassiveDamageValues((ent.Owner, rev), ent.Comp.RevenantDamageOvertime, alive);

            _admin.Add(LogType.Mind, LogImpact.High, $"{ToPrettyString(possessor)} made a revenant (possessed) out of {ToPrettyString(ent.Owner)}");
            return;
        }

        // its animateable, no reason to check for anything else
        if (HasComp<AnimateableComponent>(ent.Owner))
        {
            _mind.TransferTo(possessorMind, ent.Owner);

            ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.PossessionDuration;
            Dirty(ent);

            _admin.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(possessor)} possessed the object {ToPrettyString(ent.Owner)}");
        }
    }

    public void SetPossessorAndMind(
        Entity<WraithPossessedComponent> ent,
        EntityUid possessor,
        EntityUid possessorMind)
    {
        ent.Comp.Possessor = possessor;
        ent.Comp.PossessorMind = possessorMind;
        Dirty(ent);
    }

    public void SetPossessionDuration(Entity<WraithPossessedComponent> ent, TimeSpan duration)
    {
        ent.Comp.PossessionDuration = duration;
        Dirty(ent);
    }

    private void ReturnBack(Entity<WraithPossessedComponent> ent)
    {
        if (ent.Comp.Possessor == null || ent.Comp.PossessorMind == null)
            return;

        _mind.TransferTo(ent.Comp.PossessorMind.Value, ent.Comp.Possessor);

        var ev = new PossessionEndedEvent();
        RaiseLocalEvent(ent.Comp.Possessor.Value, ref ev);

        if (ent.Comp.OriginalMind.HasValue
            && TryComp<MindComponent>(ent.Comp.OriginalMind.Value, out var mindComp)
            && _player.TryGetSessionById(mindComp.UserId, out _))
        {
            _mind.TransferTo(ent.Comp.OriginalMind.Value, ent.Owner);
        }

        RemCompDeferred<WraithPossessedComponent>(ent.Owner);
    }
    #endregion
}
