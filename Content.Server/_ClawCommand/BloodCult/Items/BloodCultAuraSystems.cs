using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.BloodCult.Items;

/// <summary>
/// Stun and shadow shackle hand auras.
/// </summary>
public sealed partial class BloodCultAuraSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedCuffableSystem _cuffable = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly EntProtoId MutedEffect = "StatusEffectMuted";
    private const string ShacklesContainer = "shackles";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunAuraComponent, MeleeHitEvent>(OnStunHit);

        SubscribeLocalEvent<ShadowShacklesAuraComponent, MapInitEvent>(OnShacklesMapInit);
        SubscribeLocalEvent<ShadowShacklesAuraComponent, MeleeHitEvent>(OnShacklesHit);
        SubscribeLocalEvent<ShadowShacklesAuraComponent, EntRemovedFromContainerMessage>(OnShacklesUsed);
    }

    private void Incant(EntityUid user, BaseBloodCultAuraComponent aura)
    {
        if (aura.Speech is { } speech)
            _chat.TrySendInGameICMessage(user, speech, InGameICChatType.Whisper, false, checkRadioPrefix: false);
    }

    private bool IsValidVictim(EntityUid aura, EntityUid target)
    {
        return aura != target &&
               !HasComp<BloodCultistComponent>(target) &&
               !(TryComp<BloodCultConstructComponent>(target, out var construct) && !construct.Holy);
    }

    private void OnStunHit(Entity<StunAuraComponent> aura, ref MeleeHitEvent args)
    {
        if (args.HitEntities.FirstOrDefault() is not { Valid: true } target || !IsValidVictim(aura, target))
            return;

        Incant(args.User, aura.Comp);
        _statusEffects.TryAddStatusEffectDuration(target, MutedEffect, aura.Comp.MuteDuration);
        _stun.TryUpdateParalyzeDuration(target, aura.Comp.ParalyzeDuration);
        QueueDel(aura);
    }

    private void OnShacklesMapInit(Entity<ShadowShacklesAuraComponent> aura, ref MapInitEvent args)
    {
        var container = _container.EnsureContainer<Container>(aura, ShacklesContainer);
        var shackles = Spawn(aura.Comp.ShacklesProto, _transform.GetMapCoordinates(aura));
        _container.Insert(shackles, container);
        aura.Comp.Shackles = shackles;
    }

    private void OnShacklesHit(Entity<ShadowShacklesAuraComponent> aura, ref MeleeHitEvent args)
    {
        if (aura.Comp.Shackles is not { } shackles ||
            args.HitEntities.FirstOrDefault() is not { Valid: true } target ||
            !IsValidVictim(aura, target) ||
            !TryComp<CuffableComponent>(target, out var cuffable))
            return;

        aura.Comp.Target = target;

        // The shackles live in the aura's own container, not the user's hand, so TryCuffing's
        // do-after path never applies: it gates on SharedCuffableSystem.CanDrop(user, handcuff),
        // which always fails for a handcuff entity the user isn't holding. TryAddNewCuffs has no
        // such check, so the bind lands instantly on any hit, same as it already did for a downed victim.
        if (_cuffable.TryAddNewCuffs(target, args.User, shackles, cuffable))
            Incant(args.User, aura.Comp);
    }

    /// <summary>
    /// The shackles left the aura, so they are on someone now.
    /// </summary>
    private void OnShacklesUsed(Entity<ShadowShacklesAuraComponent> aura, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ShacklesContainer)
            return;

        if (aura.Comp.Target is { } target)
            _statusEffects.TryAddStatusEffectDuration(target, MutedEffect, aura.Comp.MuteDuration);

        QueueDel(aura);
    }
}

/// <summary>
/// Blood rites: soak up blood, spend it healing the cult or forging advanced rites.
/// </summary>
public sealed partial class BloodRitesSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodRitesAuraComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BloodRitesAuraComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BloodRitesAuraComponent, BloodRitesExtractDoAfterEvent>(OnExtractDoAfter);
        SubscribeLocalEvent<BloodRitesAuraComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<BloodRitesAuraComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<BloodRitesAuraComponent, BloodCultOptionSelectedMessage>(OnRiteSelected);
    }

    private void OnExamined(Entity<BloodRitesAuraComponent> rites, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("blood-rites-stored-blood", ("amount", rites.Comp.StoredBlood.Int())));
    }

    private void OnAfterInteract(Entity<BloodRitesAuraComponent> rites, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User ||
            HasComp<BloodCultistComponent>(target))
            return;

        if (HasComp<BloodstreamComponent>(target))
        {
            args.Handled = true;
            if (rites.Comp.Extracting)
                return;

            var doAfter = new DoAfterArgs(EntityManager, args.User, rites.Comp.BloodExtractionTime, new BloodRitesExtractDoAfterEvent(), rites, target, rites)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            };

            rites.Comp.Extracting = _doAfter.TryStartDoAfter(doAfter);
            return;
        }

        if (HasComp<PuddleComponent>(target))
        {
            args.Handled = true;
            ConsumePuddles(target, rites);
            return;
        }

        if (TryComp<SolutionManagerComponent>(target, out var manager))
        {
            args.Handled = true;
            if (ConsumeBloodFromSolution((target, manager), rites) > 0)
                _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
        }
    }

    private void OnExtractDoAfter(Entity<BloodRitesAuraComponent> rites, ref BloodRitesExtractDoAfterEvent args)
    {
        rites.Comp.Extracting = false;
        if (args.Cancelled || args.Handled || args.Target is not { } target ||
            !TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        args.Handled = true;

        var reference = bloodstream.BloodReferenceSolution.Volume;
        var before = _bloodstream.GetBloodLevel((target, bloodstream)) * (float) reference;
        _bloodstream.TryModifyBloodLevel((target, bloodstream), -rites.Comp.BloodExtractionAmount);
        var after = _bloodstream.GetBloodLevel((target, bloodstream)) * (float) reference;

        var extracted = FixedPoint2.Max(FixedPoint2.Zero, FixedPoint2.New(before - after));
        rites.Comp.StoredBlood += extracted;
        _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
    }

    private void OnMeleeHit(Entity<BloodRitesAuraComponent> rites, ref MeleeHitEvent args)
    {
        var playSound = false;
        foreach (var target in args.HitEntities)
        {
            if (!HasComp<BloodCultistComponent>(target) &&
                !(TryComp<BloodCultConstructComponent>(target, out var construct) && !construct.Holy))
                continue;

            if (TryComp<BloodstreamComponent>(target, out var bloodstream) && RestoreBloodLevel(rites, args.User, (target, bloodstream)))
                playSound = true;

            if (Heal(rites, args.User, target))
                playSound = true;
        }

        if (playSound)
            _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
    }

    private void OnBeforeUiOpen(Entity<BloodRitesAuraComponent> rites, ref BeforeActivatableUIOpenEvent args)
    {
        var entries = new List<BloodCultRadialEntry>();
        foreach (var (proto, cost) in rites.Comp.Crafts)
        {
            if (!_proto.TryIndex(proto, out var entity))
                continue;

            entries.Add(new BloodCultRadialEntry(proto,
                Loc.GetString("blood-rites-craft-entry", ("name", entity.Name), ("cost", cost.Int()), ("stored", rites.Comp.StoredBlood.Int())),
                proto));
        }

        _ui.SetUiState(rites.Owner, BloodCultUiKey.Rites, new BloodCultRadialState(entries));
    }

    private void OnRiteSelected(Entity<BloodRitesAuraComponent> rites, ref BloodCultOptionSelectedMessage args)
    {
        if (!args.UiKey.Equals(BloodCultUiKey.Rites))
            return;

        _ui.CloseUi(rites.Owner, BloodCultUiKey.Rites);

        var proto = new EntProtoId(args.Id);
        if (!rites.Comp.Crafts.TryGetValue(proto, out var cost))
            return;

        if (rites.Comp.StoredBlood < cost)
        {
            _popup.PopupEntity(Loc.GetString("blood-rites-not-enough-blood"), rites, args.Actor);
            return;
        }

        var item = Spawn(proto, _transform.GetMapCoordinates(args.Actor));
        QueueDel(rites);
        _hands.TryPickupAnyHand(args.Actor, item);
    }

    private bool Heal(Entity<BloodRitesAuraComponent> rites, EntityUid user, EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable) || _damageable.GetTotalDamage((target, damageable)) <= 0)
            return false;

        if (_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("blood-rites-heal-dead"), target, user);
            return false;
        }

        var cost = rites.Comp.HealingCost;
        if (target == user)
            cost *= rites.Comp.SelfHealRatio;

        if (cost > rites.Comp.StoredBlood)
        {
            _popup.PopupEntity(Loc.GetString("blood-rites-not-enough-blood"), rites, user);
            return false;
        }

        _damageable.HealEvenly((target, damageable), -rites.Comp.TotalHealing, origin: user);
        rites.Comp.StoredBlood -= cost;
        return true;
    }

    private bool RestoreBloodLevel(Entity<BloodRitesAuraComponent> rites, EntityUid user, Entity<BloodstreamComponent> target)
    {
        _bloodstream.FlushChemicals(target.AsNullable(), 10);

        var reference = target.Comp.BloodReferenceSolution.Volume;
        var missing = FixedPoint2.Max(FixedPoint2.Zero, reference - FixedPoint2.New(_bloodstream.GetBloodLevel(target.AsNullable()) * (float) reference));
        if (missing <= 0 || rites.Comp.StoredBlood <= 0)
            return false;

        var ratio = rites.Comp.BloodRegenerationRatio;
        if (target.Owner == user)
            ratio *= rites.Comp.SelfHealRatio;

        var cost = missing * ratio;
        if (cost > rites.Comp.StoredBlood)
        {
            _popup.PopupEntity(Loc.GetString("blood-rites-no-blood-left"), rites, user);
            cost = rites.Comp.StoredBlood;
        }

        _bloodstream.TryModifyBleedAmount(target.AsNullable(), -3);
        _bloodstream.TryModifyBloodLevel(target.AsNullable(), cost / ratio);
        rites.Comp.StoredBlood -= cost;
        return true;
    }

    private void ConsumePuddles(EntityUid origin, Entity<BloodRitesAuraComponent> rites)
    {
        var total = FixedPoint2.Zero;
        foreach (var puddle in _lookup.GetEntitiesInRange<PuddleComponent>(Transform(origin).Coordinates, rites.Comp.PuddleConsumeRadius, LookupFlags.Uncontained))
        {
            if (TryComp<SolutionManagerComponent>(puddle, out var manager))
                total += ConsumeBloodFromSolution((puddle, manager), rites);
        }

        if (total > 0)
            _audio.PlayPvs(rites.Comp.BloodRitesAudio, rites);
    }

    /// <summary>
    /// Pulls every kind of blood out of the entity's solutions.
    /// </summary>
    private FixedPoint2 ConsumeBloodFromSolution(Entity<SolutionManagerComponent?> ent, Entity<BloodRitesAuraComponent> rites)
    {
        var total = FixedPoint2.Zero;
        foreach (var (_, solution) in _solution.EnumerateSolutions(ent))
        {
            foreach (var reagent in solution.Comp.Solution.Contents.ToList())
            {
                if (!IsBlood(reagent.Reagent.Prototype))
                    continue;

                total += _solution.RemoveReagent(solution, reagent.Reagent, reagent.Quantity);
            }
        }

        rites.Comp.StoredBlood += total;
        return total;
    }

    private static bool IsBlood(string reagent)
    {
        return reagent.Contains("Blood", StringComparison.OrdinalIgnoreCase) || reagent == "Slime" || reagent == "Sap";
    }
}
