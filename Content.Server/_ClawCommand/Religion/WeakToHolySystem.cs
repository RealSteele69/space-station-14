// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Religion;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Religion;
using Content.Shared.Bible.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Timing;
using Content.Shared.Timing.Systems;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Timing.Components;

namespace Content.Server._ClawCommand.Religion;

/// <summary>
/// Makes <see cref="WeakToHolyComponent"/> entities that always take holy damage actually able to take it,
/// heals that holy damage over time, and lets chaplains smite or exorcise them with a bible.
/// </summary>
/// <remarks>
/// In this fork holy damage only applies to damage containers in the Metaphysical group, so normal crew are immune.
/// Weak-to-holy entities get their container swapped for its metaphysical twin while they have the component.
/// </remarks>
public sealed partial class WeakToHolySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    private static readonly ProtoId<DamageTypePrototype> Holy = "Holy";
    private static readonly ProtoId<DamageContainerPrototype> Biological = "Biological";
    private static readonly ProtoId<DamageContainerPrototype> BiologicalMetaphysical = "BiologicalMetaphysical";

    private static readonly DamageSpecifier SmiteDamage = new() { DamageDict = { ["Holy"] = 25 } };
    private static readonly TimeSpan SmiteStunDuration = TimeSpan.FromSeconds(8);
    private static readonly SoundSpecifier SizzleSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
    private const float ExorcismTime = 10f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeakToHolyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WeakToHolyComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<WeakToHolyComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnStartup(Entity<WeakToHolyComponent> ent, ref ComponentStartup args)
    {
        UpdateDamageContainer(ent);
    }

    private void OnRemove(Entity<WeakToHolyComponent> ent, ref ComponentRemove args)
    {
        if (!ent.Comp.SwappedDamageContainer || TerminatingOrDeleted(ent) || !TryComp<InjurableComponent>(ent, out var injurable))
            return;

        injurable.DamageContainer = ent.Comp.OriginalDamageContainer;
        Dirty(ent, injurable);
    }

    /// <summary>
    /// Swaps a plain biological damage container for the metaphysical one so holy damage lands.
    /// </summary>
    private void UpdateDamageContainer(Entity<WeakToHolyComponent> ent)
    {
        if (!ent.Comp.AlwaysTakeHoly
            || ent.Comp.SwappedDamageContainer
            || !TryComp<InjurableComponent>(ent, out var injurable)
            || _damageable.CanBeDamagedBy((ent, injurable), Holy))
            return;

        if (injurable.DamageContainer != Biological)
        {
            Log.Warning($"{ToPrettyString(ent)} is weak to holy, but its damage container {injurable.DamageContainer} has no holy-capable twin.");
            return;
        }

        ent.Comp.OriginalDamageContainer = injurable.DamageContainer;
        ent.Comp.SwappedDamageContainer = true;
        injurable.DamageContainer = BiologicalMetaphysical;
        Dirty(ent, injurable);
    }

    #region Bible

    private void OnInteractUsing(Entity<WeakToHolyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !ent.Comp.AlwaysTakeHoly
            || !HasComp<BibleComponent>(args.Used)
            || !HasComp<BibleUserComponent>(args.User)
            || !TryComp<UseDelayComponent>(args.Used, out var useDelay)
            || _useDelay.IsDelayed((args.Used, useDelay)))
            return;

        var target = ent.Owner;
        var multiplier = TryComp<DevilComponent>(target, out var devil) ? devil.BibleUserDamageMultiplier : 1f;

        if (!_mobState.IsIncapacitated(target))
        {
            var popup = Loc.GetString("weaktoholy-component-bible-sizzle", ("target", target), ("item", args.Used));
            _popup.PopupEntity(popup, target, PopupType.LargeCaution);
            _audio.PlayPvs(SizzleSound, target);
            _damageable.TryChangeDamage(target, SmiteDamage * multiplier, true, origin: args.Used);
            _stun.TryUpdateParalyzeDuration(target, SmiteStunDuration * multiplier);
            _useDelay.TryResetDelay((args.Used, useDelay));
            args.Handled = true;
            return;
        }

        // Only devils can be sent back to hell.
        if (devil == null)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ExorcismTime, new ExorcismDoAfterEvent(), target, target, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            BreakOnDropItem = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _popup.PopupEntity(Loc.GetString("devil-banish-begin", ("target", target), ("user", args.User)), target, PopupType.LargeCaution);
        args.Handled = true;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WeakToHolyComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var weakToHoly, out var damageable))
        {
            if (weakToHoly.NextPassiveHealTick > _timing.CurTime)
                continue;

            weakToHoly.NextPassiveHealTick = _timing.CurTime + weakToHoly.HealTickDelay;

            // AlwaysTakeHoly is usually set right after the component is added, so keep the container in sync.
            UpdateDamageContainer((uid, weakToHoly));

            if (!_damageable.GetAllDamage((uid, damageable)).DamageDict.TryGetValue(Holy, out var holy) || holy <= 0)
                continue;

            // Rune healing.
            if (weakToHoly.IsColliding)
                _damageable.TryChangeDamage(uid, weakToHoly.HealAmount, ignoreResistances: true);

            // Passive healing.
            _damageable.TryChangeDamage(uid, weakToHoly.PassiveAmount, ignoreResistances: true);
        }
    }
}
