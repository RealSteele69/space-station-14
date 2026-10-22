using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.StatusEffectNew;
using Content.Shared.Verbs;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.Body.Preening;

/// <summary>
///     Claw Command - Preening verb, feather shedding on heavy brute hits and feather regrowth for
///     <see cref="PreenableComponent"/>.
/// </summary>
public sealed partial class PreenableSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ForensicsSystem _forensics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly EntProtoId AdrenalineEffect = "StatusEffectAdrenaline";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PreenableComponent, ComponentInit>(OnCompInit);
        SubscribeLocalEvent<PreenableComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<PreenableComponent, PreeningDoAfterEvent>(OnPreened);
        SubscribeLocalEvent<PreenableComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<PreenableComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnCompInit(Entity<PreenableComponent> ent, ref ComponentInit args)
    {
        ent.Comp.CurrentFeathers = ent.Comp.MaximumFeathers;
        Dirty(ent);
    }

    private void OnGetVerbs(Entity<PreenableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // nothing left to preen
        if (ent.Comp.CurrentFeathers <= 0)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Act = () => StartPreening(ent, user),
            Text = Loc.GetString(ent.Comp.PreeningVerbString),
        });
    }

    private void StartPreening(Entity<PreenableComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.PreenDelay, new PreeningDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        if (user == ent.Owner)
        {
            _popup.PopupClient(Loc.GetString(ent.Comp.SelfPreeningMessage), ent, ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString(ent.Comp.GettingPreenedMessage, ("preener", Identity.Entity(user, EntityManager))), ent, ent, PopupType.Medium);
        _popup.PopupClient(Loc.GetString(ent.Comp.PreeningOtherMessage, ("preenee", Identity.Entity(ent, EntityManager))), user, user);
    }

    private void OnPreened(Entity<PreenableComponent> ent, ref PreeningDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (ent.Comp.CurrentFeathers <= 0)
            return;

        args.Handled = true;
        var feather = SpawnFeather(ent, false);
        _hands.TryPickupAnyHand(args.User, feather);
    }

    private void OnDamageDealt(Entity<PreenableComponent> ent, ref DamageDealtEvent args)
    {
        if (ent.Comp.CurrentFeathers <= 0)
            return;

        var applicable = FixedPoint2.Zero;
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value > 0 && ent.Comp.ValidDamageTypes.Contains(type))
                applicable += value;
        }

        if (applicable <= ent.Comp.ShedDamageThreshold)
            return;

        // predicted so the client and server agree on whether the feather came off
        var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent));
        if (rand.NextDouble() >= (applicable * ent.Comp.ShedScalingChance).Double())
            return;

        var feather = SpawnFeather(ent, true);

        // fling it off the body
        var scatter = _random.NextAngle().ToVec() * _random.NextFloat(10, 40);
        _physics.ApplyLinearImpulse(feather, scatter);
        _physics.ApplyAngularImpulse(feather, _random.NextFloat(-30, 30));

        var meta = MetaData(feather);
        _metaData.SetEntityName(feather, Loc.GetString(ent.Comp.FeatherBloodiedNameString, ("item", Name(feather))), meta);
        _metaData.SetEntityDescription(feather, Loc.GetString(ent.Comp.FeatherBloodiedDescString), meta);

        _popup.PopupClient(Loc.GetString(ent.Comp.DroppedFeatherString), ent, ent, PopupType.MediumCaution);
        _chat.TryEmoteWithoutChat(ent, ent.Comp.ScreamEmote);
        _statusEffects.TryUpdateStatusEffectDuration(ent, AdrenalineEffect, TimeSpan.FromSeconds(3));
    }

    private void OnDamageModify(Entity<PreenableComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.VulnerabilityModifier is not { } modifier || ent.Comp.MaximumFeathers <= 0)
            return;

        // no effect at full feathers, the full modifier at none
        var missing = 1f - ent.Comp.CurrentFeathers / (float) ent.Comp.MaximumFeathers;
        if (missing <= 0f)
            return;

        var scaled = new DamageModifierSet();
        foreach (var (type, coefficient) in modifier.Coefficients)
        {
            scaled.Coefficients[type] = 1f + (coefficient - 1f) * missing;
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, scaled);
    }

    private EntityUid SpawnFeather(Entity<PreenableComponent> ent, bool bloody)
    {
        var feather = PredictedSpawnAtPosition(ent.Comp.FeatherPrototype, Transform(ent).Coordinates);

        _appearance.SetData(feather, FeatherVisuals.FeatherColor, GetSkinColor(ent));

        // best be careful, no cleaning this
        _forensics.TransferDna(feather, ent, false);

        ent.Comp.CurrentFeathers -= 1;
        ent.Comp.ReplenishTime = _timing.CurTime + ent.Comp.ReplenishDelay;
        Dirty(ent);

        if (bloody && TryComp<BloodstreamComponent>(ent, out var bloodstream) && bloodstream.BloodSolution is { } blood)
            _appearance.SetData(feather, FeatherVisuals.BloodColor, blood.Comp.Solution.GetColor(_prototype));

        return feather;
    }

    /// <summary>
    ///     The skin colour lives on the visual organs; any non-eye organ carries it.
    /// </summary>
    private Color GetSkinColor(EntityUid uid)
    {
        foreach (var organ in _body.EnumerateOrgans<VisualOrganComponent>(uid))
        {
            if (organ.Comp2.Layer is not HumanoidVisualLayers.Eyes)
                return organ.Comp2.Profile.SkinColor;
        }

        return Color.White;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PreenableComponent>();
        while (query.MoveNext(out var uid, out var preenable))
        {
            if (preenable.ReplenishTime is not { } time || _timing.CurTime < time)
                continue;

            preenable.CurrentFeathers = Math.Min(preenable.CurrentFeathers + 1, preenable.MaximumFeathers);
            preenable.ReplenishTime = preenable.CurrentFeathers >= preenable.MaximumFeathers
                ? null
                : _timing.CurTime + preenable.ReplenishDelay;
            Dirty(uid, preenable);
        }
    }
}
