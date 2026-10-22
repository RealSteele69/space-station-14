// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._ClawCommand.Lavaland.Aggression;
using Content.Shared._ClawCommand.Lavaland.Megafauna.Events;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Lavaland.Megafauna.Systems;

/// <summary>
/// Generic boss attacks used by the lavaland megafauna AI: projectile patterns,
/// charges, swoops, shouts and aggro-on-sight.
/// </summary>
public sealed partial class MegafaunaAttacksSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AggressorsSystem _aggressors = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeProjectiles();
        InitializeCharge();
        InitializeSwoop();
        InitializeAggro();

        SubscribeLocalEvent<ActionsComponent, MegafaunaShoutActionEvent>(OnShoutAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        UpdateProjectiles(curTime);
        UpdateCharge(curTime, frameTime);
        UpdateSwoop(curTime, frameTime);
        UpdateAggro(curTime);
    }

    private void OnShoutAction(Entity<ActionsComponent> ent, ref MegafaunaShoutActionEvent args)
    {
        if (args.Handled || args.Lines.Count == 0)
            return;

        Shout(ent, _random.Pick(args.Lines));
        args.Handled = true;
    }

    private void Shout(EntityUid uid, LocId? line)
    {
        if (line == null)
            return;

        _chat.TrySendInGameICMessage(uid, Loc.GetString(line.Value), InGameICChatType.Speak, false, ignoreActionBlocker: true);
    }

    /// <summary>
    /// Puts the boss' HTN to sleep while an attack moves it around, so steering doesn't fight the attack.
    /// </summary>
    private bool PauseNpc(EntityUid uid)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return false;

        var awake = _npc.IsAwake(uid, htn);
        if (awake)
            _npc.SleepNPC(uid, htn);

        return awake;
    }

    private void ResumeNpc(EntityUid uid, bool wasAwake)
    {
        if (!wasAwake || _mobState.IsDead(uid))
            return;

        _npc.WakeNPC(uid);
    }
}
