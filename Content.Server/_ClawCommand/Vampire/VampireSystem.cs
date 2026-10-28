// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._ClawCommand.Shadekin;
using Content.Server.Antag.Components;
using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Overlays;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Vampire;

// TODO (not yet ported - picked up again later):
// - Veil of Many Faces: full appearance/name randomization. This fork has no HumanoidAppearanceComponent
//   (Goob's own appearance API diverges here), so this needs a dedicated look at whatever replaced it
//   before it can be built - ChangelingTransformSystem is the closest existing reference.
// - Brawn: break cuffs/pulls for free, else punch with a knockdown chance, or pry open a door. Door-pry
//   and cuff-break both have real APIs (SharedCuffableSystem.Uncuff, airlock pry-open), but it's a
//   three-way branching power and deserves its own pass rather than being rushed in.
// - Immortal Haste: a short ramming dash that knocks down anyone it passes through.
// - Predatory Lunge: a tackle-on-approach power.
// - Trespass: a brief, short-range walk-through-walls teleport.
//   Haste/Lunge/Trespass all need a dash/tackle/wall-phase movement primitive this engine has no
//   confirmed reusable component for - worth a real research pass before attempting any of them.
//
// Smaller simplifications already made on purpose, not bugs:
// - Vassal conversion is a single DoAfter ("Embrace") on an incapacitated victim, not the source's
//   vassal-rack multi-stage torture ritual.
// - Vassals sense their master via an on-demand distance popup, not a live pinpointer-style HUD.
// - The candelabrum doesn't swap its sprite between lit/unlit (would need an Appearance/Visualizer
//   layer) and doesn't cause hallucinations (no confirmed hallucination API in this fork) - it still
//   toggles its light and the mood debuff correctly.
public sealed partial class VampireSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<VampireComponent, ComponentShutdown>(OnShutdown);

        InitializeFeed();
        InitializeMasquerade();
        InitializeFortitude();
        InitializeCloak();
        InitializeCoffin();
        InitializeRankUp();
        InitializeMesmerize();
        InitializeVassal();
        InitializeStake();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<VampireComponent>();
        while (query.MoveNext(out var uid, out var vampire))
        {
            if (now < vampire.NextTick)
                continue;

            vampire.NextTick = now + vampire.TickInterval;
            LifeTick((uid, vampire));
        }

        var recuperateQuery = EntityQueryEnumerator<VassalRecuperatingComponent>();
        while (recuperateQuery.MoveNext(out var uid, out var recuperating))
        {
            if (now < recuperating.NextTick)
                continue;

            recuperating.NextTick = now + TimeSpan.FromSeconds(1);
            RecuperateTick((uid, recuperating));
        }

        var stakedQuery = EntityQueryEnumerator<VampireStakedComponent>();
        while (stakedQuery.MoveNext(out var uid, out var staked))
            UnstakeIfExpired((uid, staked), now);
    }

    private void OnStartup(Entity<VampireComponent> vampire, ref MapInitEvent args)
    {
        // The dead don't eat, drink, breathe or catch the station's diseases - only blood sustains them now.
        RemComp<SatiationComponent>(vampire);
        EnsureComp<RespiratorImmuneComponent>(vampire);
        EnsureComp<ZombieImmuneComponent>(vampire);
        EnsureComp<AntagImmuneComponent>(vampire);
        EnsureComp<NightVisionComponent>(vampire);

        if (TryComp<DamageableComponent>(vampire, out var damageable))
            _damageable.SetDamageModifierSetId((vampire, damageable), vampire.Comp.VampireDamageModifierSet);

        vampire.Comp.ActionEntities = new List<EntityUid>();
        foreach (var actionId in vampire.Comp.BaseVampireActions)
        {
            if (_actions.AddAction(vampire, actionId) is { } action)
                vampire.Comp.ActionEntities.Add(action);
        }

        UpdateBloodAlert(vampire);
    }

    private void OnShutdown(Entity<VampireComponent> vampire, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(vampire.Owner, vampire.Comp.BloodAlert);
    }

    /// <summary>
    /// Adds blood points (capped at the vampire's current maximum), stolen from a victim or granted some other way.
    /// </summary>
    public void AddBloodPoints(Entity<VampireComponent> vampire, float amount)
    {
        vampire.Comp.BloodPoints = Math.Clamp(vampire.Comp.BloodPoints + amount, 0f, vampire.Comp.MaxBloodPoints);
        UpdateBloodAlert(vampire);
    }

    public void UpdateBloodAlert(Entity<VampireComponent> vampire)
    {
        var ratio = vampire.Comp.MaxBloodPoints > 0f
            ? vampire.Comp.BloodPoints / vampire.Comp.MaxBloodPoints
            : 0f;

        // Reuses the bleed-out icon ramp: a full blood supply shows the calm "not bleeding" state,
        // while a drained vampire shows the same alarming red as someone bleeding to death.
        var severity = (short) Math.Clamp((int) Math.Round((1f - ratio) * 10f), 0, 10);
        _alerts.ShowAlert(vampire.Owner, vampire.Comp.BloodAlert, severity);
    }
}
