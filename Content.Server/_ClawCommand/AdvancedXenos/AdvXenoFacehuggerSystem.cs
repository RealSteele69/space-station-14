using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.Bed.Sleep;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Humanoid;
using Content.Shared.Silicon.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Server.Administration.Logs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - the facehugger. Latches onto faces, knocks the host out,
/// and implants an embryo if nobody pulls it off in time. Helmets, unremovable masks and synthetic bodies stop it.
/// </summary>
public sealed partial class AdvXenoFacehuggerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public const string MaskSlot = "mask";
    public const string HeadSlot = "head";

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<TagPrototype> HuggerProofTag = "AdvXenoHuggerProof";

    // MIN/MAX_IMPREGNATION_TIME and MIN/MAX_ACTIVE_TIME, in seconds
    private const float MinImpregnation = 10f;
    private const float MaxImpregnation = 15f;
    private const float MinIdle = 20f;
    private const float MaxIdle = 40f;

    /// <summary>Huggers mid-skitter, so landing does not count as being thrown.</summary>
    private readonly HashSet<EntityUid> _crawling = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoFacehuggerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AdvXenoFacehuggerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AdvXenoFacehuggerComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<AdvXenoFacehuggerComponent, StepTriggeredOffEvent>(OnStepped);
        SubscribeLocalEvent<AdvXenoFacehuggerComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<AdvXenoFacehuggerComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<AdvXenoFacehuggerComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<AdvXenoFacehuggerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<AdvXenoFacehuggerComponent> ent, ref MapInitEvent args)
    {
        // pixel_x = rand(6,-6): a little scatter so a clutch of them does not stack perfectly
        ent.Comp.NextCrawl = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(1f, 3f));
        UpdateVisuals(ent);
    }

    private void OnShutdown(Entity<AdvXenoFacehuggerComponent> ent, ref ComponentShutdown args)
    {
        _crawling.Remove(ent);
    }

    #region Who can be hugged

    /// <summary>
    /// /proc/CanHug: a humanoid of a playable species with a face to grab (a mask slot), no hive node, an organic body, and no sealed helmet.
    /// </summary>
    public bool CanHug(EntityUid target)
    {
        if (!HasComp<MobStateComponent>(target) || HasComp<AdvXenoHiveComponent>(target) || !IsValidHost(target))
            return false;

        if (!_inventory.HasSlot(target, MaskSlot))
            return false;

        return !IsFaceSealed(target);
    }

    /// <summary>
    /// Only organic humanoids of a playable species can carry an embryo. Animals, pets, borgs and other
    /// non-humanoid mobs, non-player species (skeletons, gingerbread) and synthetic bodies (IPC, synth) can't,
    /// so huggers don't go for them at all.
    /// </summary>
    public bool IsValidHost(EntityUid target)
    {
        if (!TryComp<HumanoidProfileComponent>(target, out var profile)
            || !_proto.TryIndex(profile.Species, out var species) || !species.RoundStart)
            return false;

        return !HasComp<AdvXenoHostImmuneComponent>(target) && !HasComp<SiliconComponent>(target);
    }

    /// <summary>
    /// A head item covering the face that is not flexible: hardsuit and space helmets, welding and riot helmets
    /// with the visor down, anything blocking the mouth. Also a hugger-proof mask worn over the face (welding gas mask).
    /// </summary>
    public bool IsFaceSealed(EntityUid target)
    {
        return IsFaceSealed(target, out _);
    }

    public bool IsFaceSealed(EntityUid target, [NotNullWhen(true)] out EntityUid? cover)
    {
        cover = null;

        if (_inventory.TryGetSlotEntity(target, HeadSlot, out var head))
        {
            if (HasComp<PressureProtectionComponent>(head) || _tag.HasTag(head.Value, HuggerProofTag)
                || TryComp<IngestionBlockerComponent>(head, out var blocker) && blocker.Enabled)
            {
                cover = head;
                return true;
            }
        }

        // a mask only counts while it is up over the face, not pulled down
        if (_inventory.TryGetSlotEntity(target, MaskSlot, out var mask) && _tag.HasTag(mask.Value, HuggerProofTag)
            && (!TryComp<IngestionBlockerComponent>(mask, out var maskBlocker) || maskBlocker.Enabled))
        {
            cover = mask;
            return true;
        }

        return false;
    }

    #endregion

    #region Triggers

    // legacy_mob_melee_hook: hitting someone with a live hugger hands it to them.
    private void OnMeleeHit(Entity<AdvXenoFacehuggerComponent> ent, ref MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            if (!HasComp<MobStateComponent>(hit) || hit == args.User)
                continue;

            _hands.TryDrop(args.User, ent.Owner, checkActionBlocker: false);
            TryAttach(ent, hit);
            break;
        }
    }

    private void OnStepAttempt(Entity<AdvXenoFacehuggerComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = ent.Comp.State == AdvXenoHuggerState.Active && CanHug(args.Tripper);
    }

    // Crossed(): walking over a live hugger.
    private void OnStepped(Entity<AdvXenoFacehuggerComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (CanHug(args.Tripper))
            TryAttach(ent, args.Tripper);
    }

    private void OnThrown(Entity<AdvXenoFacehuggerComponent> ent, ref ThrownEvent args)
    {
        if (_crawling.Contains(ent) || ent.Comp.State != AdvXenoHuggerState.Active)
            return;

        _appearance.SetData(ent, AdvXenoHuggerVisuals.State, AdvXenoHuggerSprite.Thrown);
    }

    // throw_impact: stunned for a few seconds, good for positioning but not as a weapon.
    private void OnLand(Entity<AdvXenoFacehuggerComponent> ent, ref LandEvent args)
    {
        if (_crawling.Remove(ent))
            return;

        UpdateVisuals(ent);
        GoIdle(ent, 3f, 10f);
    }

    // attackby with any force, bullet_act, fire_act above 80C: it dies.
    private void OnDamaged(Entity<AdvXenoFacehuggerComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            Die(ent);
    }

    #endregion

    #region State

    public void GoIdle(Entity<AdvXenoFacehuggerComponent> ent, float min = MinIdle, float max = MaxIdle)
    {
        if (ent.Comp.State != AdvXenoHuggerState.Active)
            return;

        ent.Comp.State = AdvXenoHuggerState.Idle;
        ent.Comp.WakeAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(min, max));
        Dirty(ent);
        UpdateVisuals(ent);
    }

    public void GoActive(Entity<AdvXenoFacehuggerComponent> ent)
    {
        ent.Comp.WakeAt = null;
        if (ent.Comp.State != AdvXenoHuggerState.Idle)
            return;

        ent.Comp.State = AdvXenoHuggerState.Active;
        Dirty(ent);
        UpdateVisuals(ent);
    }

    public void Die(EntityUid uid)
    {
        if (!TryComp<AdvXenoFacehuggerComponent>(uid, out var comp))
            return;

        Die((uid, comp));
    }

    public void Die(Entity<AdvXenoFacehuggerComponent> ent)
    {
        if (ent.Comp.State is AdvXenoHuggerState.Dead or AdvXenoHuggerState.Impregnated)
            return;

        ent.Comp.State = AdvXenoHuggerState.Dead;
        ent.Comp.WakeAt = null;
        Dirty(ent);
        UpdateVisuals(ent);
        _popup.PopupEntity(Loc.GetString("adv-xeno-hugger-dies", ("hugger", ent.Owner)), ent, PopupType.Small);
    }

    private void UpdateVisuals(Entity<AdvXenoFacehuggerComponent> ent)
    {
        var (sprite, prefix) = ent.Comp.State switch
        {
            AdvXenoHuggerState.Idle => (AdvXenoHuggerSprite.Idle, "inactive"),
            AdvXenoHuggerState.Dead => (AdvXenoHuggerSprite.Dead, "dead"),
            AdvXenoHuggerState.Impregnated => (AdvXenoHuggerSprite.Impregnated, "impregnated"),
            _ => (AdvXenoHuggerSprite.Active, (string?) null),
        };

        _appearance.SetData(ent, AdvXenoHuggerVisuals.State, sprite);
        _clothing.SetEquippedPrefix(ent, prefix);
    }

    #endregion

    #region Attach / impregnate

    /// <summary>
    /// /obj/item/clothing/mask/facehugger/proc/Attach
    /// </summary>
    public bool TryAttach(EntityUid hugger, EntityUid target)
    {
        if (!TryComp<AdvXenoFacehuggerComponent>(hugger, out var comp))
            return false;

        Entity<AdvXenoFacehuggerComponent> ent = (hugger, comp);
        var now = _timing.CurTime;

        if (!HasComp<MobStateComponent>(target) || !_inventory.HasSlot(target, MaskSlot) || HasComp<AdvXenoHiveComponent>(target))
            return false;

        // checked before the damage below: something that can't host takes nothing from a hugger
        if (!IsValidHost(target))
        {
            if (comp.State == AdvXenoHuggerState.Active)
                _popup.PopupEntity(Loc.GetString("adv-xeno-hugger-synthetic", ("hugger", hugger), ("target", Identity.Entity(target, EntityManager))), target, PopupType.Small);
            return false;
        }

        if (now < comp.AttachCooldownEnd)
            return false;
        comp.AttachCooldownEnd = now + TimeSpan.FromSeconds(MaxImpregnation);

        if (_inventory.TryGetSlotEntity(target, MaskSlot, out var worn) && worn == hugger)
            return false;
        if (comp.State != AdvXenoHuggerState.Active)
            return false;

        // done before the checks below so that even people in helmets take damage
        if (!comp.Sterile)
            _damage.TryChangeDamage(target, new DamageSpecifier(_proto.Index(Blunt), comp.Strength), origin: hugger);

        var targetName = Identity.Entity(target, EntityManager);
        _popup.PopupEntity(Loc.GetString("adv-xeno-hugger-leaps", ("hugger", hugger), ("target", targetName)), target, PopupType.LargeCaution);

        if (IsFaceSealed(target, out var cover))
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-hugger-helmet", ("hugger", hugger), ("target", targetName), ("helmet", cover.Value)), target, PopupType.MediumCaution);
            Die(ent);
            return false;
        }

        if (_inventory.TryGetSlotEntity(target, MaskSlot, out var mask))
        {
            if (!_inventory.TryUnequip(target, MaskSlot, silent: true))
            {
                _popup.PopupEntity(Loc.GetString("adv-xeno-hugger-bounce", ("hugger", hugger), ("target", targetName)), target, PopupType.MediumCaution);
                Die(ent);
                return false;
            }

            _popup.PopupEntity(Loc.GetString("adv-xeno-hugger-tears-mask", ("hugger", hugger), ("target", targetName), ("mask", mask.Value)), target, PopupType.MediumCaution);
        }

        if (_container.TryGetContainingContainer(hugger, out var holder))
            _container.Remove(hugger, holder, force: true);

        if (!_inventory.TryEquip(target, hugger, MaskSlot, silent: true, force: true))
            return false;

        _adminLog.Add(LogType.Action, LogImpact.High, $"Facehugger {ToPrettyString(hugger)} latched onto {ToPrettyString(target):target}");

        // afflict_unconscious(20 * MAX_IMPREGNATION_TIME/6): about 50 seconds out cold
        if (!comp.Sterile)
            _status.TryAddStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(MaxImpregnation * 20 / 6));

        // so it doesn't jump the people that tear it off
        GoIdle(ent);

        comp.ImpregnateTarget = target;
        comp.ImpregnateAt = now + TimeSpan.FromSeconds(_random.NextFloat(MinImpregnation, MaxImpregnation));
        Dirty(ent);
        return true;
    }

    // /obj/item/clothing/mask/facehugger/proc/Impregnate
    private void Impregnate(Entity<AdvXenoFacehuggerComponent> ent)
    {
        var target = ent.Comp.ImpregnateTarget;
        ent.Comp.ImpregnateTarget = null;
        ent.Comp.ImpregnateAt = null;

        // was taken off or something
        if (target is not { } host || TerminatingOrDeleted(host) || _mobState.IsDead(host))
            return;
        if (!_inventory.TryGetSlotEntity(host, MaskSlot, out var worn) || worn != ent.Owner)
            return;

        var hostName = Identity.Entity(host, EntityManager);
        if (ent.Comp.Sterile)
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-hugger-sterile", ("hugger", ent.Owner), ("target", hostName)), host, PopupType.MediumCaution);
            return;
        }

        EnsureComp<AdvXenoEmbryoComponent>(host);
        _popup.PopupEntity(Loc.GetString("adv-xeno-hugger-impregnates", ("hugger", ent.Owner), ("target", hostName)), host, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.High, $"Facehugger {ToPrettyString(ent)} implanted an alien embryo in {ToPrettyString(host):target}");

        ent.Comp.State = AdvXenoHuggerState.Impregnated;
        ent.Comp.WakeAt = null;
        Dirty(ent);
        UpdateVisuals(ent);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<AdvXenoFacehuggerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            Entity<AdvXenoFacehuggerComponent> ent = (uid, comp);

            if (comp.PendingAttach is { } grabber)
            {
                comp.PendingAttach = null;
                if (!TerminatingOrDeleted(grabber))
                    TryAttach(uid, grabber);
            }

            if (comp.ImpregnateAt is { } impregnateAt && now >= impregnateAt)
                Impregnate(ent);

            if (comp.WakeAt is { } wakeAt && now >= wakeAt)
                GoActive(ent);

            if (comp.State != AdvXenoHuggerState.Active || now < comp.NextCrawl)
                continue;

            comp.NextCrawl = now + TimeSpan.FromSeconds(2);
            Crawl((uid, comp, xform));
        }
    }

    // proc/crawl: on the floor, now and then it skitters to a random spot within five tiles.
    private void Crawl(Entity<AdvXenoFacehuggerComponent, TransformComponent> ent)
    {
        if (ent.Comp2.GridUid == null || ent.Comp2.ParentUid != ent.Comp2.GridUid || !_random.Prob(0.25f))
            return;

        var offset = new Vector2(_random.NextFloat(-5f, 5f), _random.NextFloat(-5f, 5f));
        if (offset.LengthSquared() < 1f)
            return;

        _crawling.Add(ent);
        if (!_throwing.TryThrow(ent, offset, 2.5f, null, pushbackRatio: 0f, recoil: false, animated: false, playSound: false, doSpin: false))
        {
            _crawling.Remove(ent);
            return;
        }

        if (_random.Prob(0.25f))
        {
            var how = _random.Pick(new[] { "adv-xeno-hugger-skitters-away", "adv-xeno-hugger-skitters-around", "adv-xeno-hugger-skitters" });
            _popup.PopupEntity(Loc.GetString(how, ("hugger", ent.Owner)), ent, PopupType.Small);
        }
    }
}
