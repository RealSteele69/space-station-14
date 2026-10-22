using Content.Server._ClawCommand.BloodCult.Runes;
using Content.Server.Chat.Systems;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.Speech.Muting;
using Content.Shared.Stacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.Whitelist;
using Content.Shared.Mind;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.BloodCult.Spells;

/// <summary>
/// Blood magic: preparing and dispelling spells, spending their charges, and what each spell does.
/// </summary>
public sealed partial class BloodCultSpellsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private CultRuneTeleportSystem _runeTeleport = default!;
    [Dependency] private CultRuneBaseSystem _cultRune = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private const string AddPrefix = "add:";
    private const string RemovePrefix = "remove:";

    private static readonly SoundSpecifier PrepareSound = new SoundPathSpecifier("/Audio/_ClawCommand/BloodCult/bloodcult_scribe.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultSpellComponent, ActionAttemptEvent>(OnSpellAttempt);
        SubscribeLocalEvent<BloodCultSpellComponent, ActionPerformedEvent>(OnSpellPerformed, after: [typeof(SharedChargesSystem)]);

        SubscribeLocalEvent<BloodCultSpellsHolderComponent, BloodCultPrepareSpellsEvent>(OnPrepareSpells);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, BloodCultOptionSelectedMessage>(OnOptionSelected);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, BloodCultCreateSpellDoAfterEvent>(OnSpellCreated);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, BloodCultTeleportDoAfterEvent>(OnTeleportDoAfter);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, BloodCultTwistedConstructionDoAfterEvent>(OnTwistedConstructionDoAfter);

        SubscribeLocalEvent<BloodCultEmpEvent>(OnEmp);
        SubscribeLocalEvent<BloodCultSummonEquipmentEvent>(OnSummonEquipment);
        SubscribeLocalEvent<BloodCultTeleportEvent>(OnTeleport);
        SubscribeLocalEvent<BloodCultTwistedConstructionEvent>(OnTwistedConstruction);
    }

    #region Spell lifecycle

    private void OnSpellAttempt(Entity<BloodCultSpellComponent> spell, ref ActionAttemptEvent args)
    {
        if (args.Cancelled || !spell.Comp.RequiresSpeech || !_statusEffects.HasEffectComp<MutedStatusEffectComponent>(args.User))
            return;

        args.Cancelled = true;
        args.Reason = Loc.GetString("blood-cult-spells-muted");
    }

    private void OnSpellPerformed(Entity<BloodCultSpellComponent> spell, ref ActionPerformedEvent args)
    {
        if (!TryComp<LimitedChargesComponent>(spell, out var charges) || !_charges.IsEmpty((spell, charges)))
            return;

        if (TryComp<BloodCultSpellsHolderComponent>(args.Performer, out var holder))
            holder.SelectedSpells.Remove(spell);

        _actions.RemoveAction(args.Performer, spell.Owner);
    }

    private void OnPrepareSpells(Entity<BloodCultSpellsHolderComponent> cultist, ref BloodCultPrepareSpellsEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var entries = new List<BloodCultRadialEntry>();
        if (cultist.Comp.SelectedSpells.Count < cultist.Comp.MaxSpells)
        {
            foreach (var spell in cultist.Comp.AvailableSpells)
            {
                if (!_proto.TryIndex(spell, out var proto))
                    continue;

                entries.Add(new BloodCultRadialEntry(AddPrefix + spell, Loc.GetString("blood-cult-spells-prepare", ("spell", proto.Name)), spell));
            }
        }

        foreach (var spell in cultist.Comp.SelectedSpells)
        {
            if (TerminatingOrDeleted(spell))
                continue;

            var proto = MetaData(spell).EntityPrototype;
            entries.Add(new BloodCultRadialEntry(RemovePrefix + GetNetEntity(spell),
                Loc.GetString("blood-cult-spells-dispel", ("spell", Name(spell))),
                proto?.ID));
        }

        if (entries.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-spells-too-many"), cultist, cultist, PopupType.Medium);
            return;
        }

        _ui.SetUiState(cultist.Owner, BloodCultUiKey.Spells, new BloodCultRadialState(entries));
        _ui.TryToggleUi(cultist.Owner, BloodCultUiKey.Spells, cultist);
    }

    private void OnOptionSelected(Entity<BloodCultSpellsHolderComponent> cultist, ref BloodCultOptionSelectedMessage args)
    {
        if (args.Actor != cultist.Owner)
            return;

        switch (args.UiKey)
        {
            case BloodCultUiKey.Spells:
                _ui.CloseUi(cultist.Owner, BloodCultUiKey.Spells);
                OnSpellOption(cultist, args.Id);
                break;
            case BloodCultUiKey.TeleportSpell:
                _ui.CloseUi(cultist.Owner, BloodCultUiKey.TeleportSpell);
                OnTeleportDestination(cultist, args.Id);
                break;
        }
    }

    private void OnSpellOption(Entity<BloodCultSpellsHolderComponent> cultist, string id)
    {
        if (id.StartsWith(RemovePrefix))
        {
            if (!NetEntity.TryParse(id[RemovePrefix.Length..], out var netSpell) ||
                !TryGetEntity(netSpell, out var spell) ||
                !cultist.Comp.SelectedSpells.Remove(spell.Value))
                return;

            _actions.RemoveAction(cultist.Owner, spell.Value);
            return;
        }

        if (!id.StartsWith(AddPrefix))
            return;

        var spellId = new EntProtoId(id[AddPrefix.Length..]);
        if (!cultist.Comp.AvailableSpells.Contains(spellId))
            return;

        if (cultist.Comp.SelectedSpells.Count >= cultist.Comp.MaxSpells)
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-spells-too-many"), cultist, cultist, PopupType.Medium);
            return;
        }

        var time = cultist.Comp.SpellCreationTime;
        if (TryComp<BloodCultEmpoweredComponent>(cultist, out var empowered))
            time *= empowered.RuneTimeMultiplier;

        var doAfter = new DoAfterArgs(EntityManager, cultist.Owner, time, new BloodCultCreateSpellDoAfterEvent(spellId), cultist.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(PrepareSound, cultist, AudioParams.Default.WithMaxDistance(3f));
    }

    private void OnSpellCreated(Entity<BloodCultSpellsHolderComponent> cultist, ref BloodCultCreateSpellDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (cultist.Comp.SelectedSpells.Count >= cultist.Comp.MaxSpells)
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-spells-too-many"), cultist, cultist, PopupType.Medium);
            return;
        }

        var damage = new Content.Shared.Damage.DamageSpecifier(cultist.Comp.SpellCreationDamage);
        if (TryComp<BloodCultEmpoweredComponent>(cultist, out var empowered))
            damage *= empowered.RuneDamageMultiplier;

        _damageable.TryChangeDamage(cultist.Owner, damage, true);

        if (_actions.AddAction(cultist, args.Spell) is { } action)
            cultist.Comp.SelectedSpells.Add(action);
    }

    #endregion

    #region Spells

    private void Incant(EntityUid caster, IBloodCultIncantation spell)
    {
        if (spell.Speech is { } speech)
            _chat.TrySendInGameICMessage(caster, speech, InGameICChatType.Whisper, false, checkRadioPrefix: false);
    }

    private void OnEmp(BloodCultEmpEvent ev)
    {
        if (ev.Handled)
            return;

        _emp.EmpPulse(_transform.GetMapCoordinates(ev.Performer), ev.Range, ev.EnergyConsumption, ev.Duration, ev.Performer);
        Incant(ev.Performer, ev);
        ev.Handled = true;
    }

    private void OnSummonEquipment(BloodCultSummonEquipmentEvent ev)
    {
        if (ev.Handled)
            return;

        var coords = _transform.GetMapCoordinates(ev.Performer);
        foreach (var (slot, protoId) in ev.Prototypes)
        {
            var entity = Spawn(protoId, coords);

            if (HasComp<ClothingComponent>(entity) && _inventory.HasSlot(ev.Performer, slot))
            {
                _inventory.TryUnequip(ev.Performer, slot, force: true);
                if (_inventory.TryEquip(ev.Performer, entity, slot, force: true))
                    continue;
            }

            if (_hands.TryPickupAnyHand(ev.Performer, entity))
                continue;

            if (!ev.Force)
            {
                _popup.PopupEntity(Loc.GetString("blood-cult-spells-no-empty-hand"), ev.Performer, ev.Performer);
                QueueDel(entity);
                return;
            }
        }

        Incant(ev.Performer, ev);
        ev.Handled = true;
    }

    private void OnTeleport(BloodCultTeleportEvent ev)
    {
        if (ev.Handled || !TryComp<BloodCultSpellsHolderComponent>(ev.Performer, out var holder))
            return;

        if (HasComp<MindShieldComponent>(ev.Target))
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-spells-protected"), ev.Performer, ev.Performer);
            return;
        }

        if (!_runeTeleport.TryGetTeleportRunes(ev.Performer, out var runes))
            return;

        holder.PendingTeleportTarget = ev.Target;
        holder.PendingTeleportDuration = ev.DoAfterDuration;

        _ui.SetUiState(ev.Performer, BloodCultUiKey.TeleportSpell, new BloodCultListState(runes));
        _ui.OpenUi(ev.Performer, BloodCultUiKey.TeleportSpell, ev.Performer);

        // The charge is only spent once the teleport goes through.
    }

    private void OnTeleportDestination(Entity<BloodCultSpellsHolderComponent> cultist, string id)
    {
        if (cultist.Comp.PendingTeleportTarget is not { } target || TerminatingOrDeleted(target) ||
            !NetEntity.TryParse(id, out var netRune) || !TryGetEntity(netRune, out var rune))
            return;

        cultist.Comp.PendingTeleportTarget = null;

        var doAfter = new DoAfterArgs(EntityManager,
            cultist.Owner,
            cultist.Comp.PendingTeleportDuration,
            new BloodCultTeleportDoAfterEvent(GetNetEntity(rune.Value)),
            cultist.Owner,
            target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnTeleportDoAfter(Entity<BloodCultSpellsHolderComponent> cultist, ref BloodCultTeleportDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target ||
            !TryGetEntity(args.Rune, out var rune) || TerminatingOrDeleted(rune))
            return;

        args.Handled = true;

        foreach (var spell in cultist.Comp.SelectedSpells)
        {
            if (_actions.GetEvent(spell) is BloodCultTeleportEvent teleport)
            {
                Incant(cultist, teleport);
                break;
            }
        }

        _cultRune.TeleportTo(target, rune.Value);
        SpendSpell<BloodCultTeleportEvent>(cultist);
    }

    private void OnTwistedConstruction(BloodCultTwistedConstructionEvent ev)
    {
        if (ev.Handled)
            return;

        foreach (var recipe in ev.Recipes)
        {
            if (!Matches(recipe, ev.Target))
                continue;

            var doAfter = new DoAfterArgs(EntityManager,
                ev.Performer,
                recipe.Delay,
                new BloodCultTwistedConstructionDoAfterEvent(recipe.Replacement),
                ev.Performer,
                ev.Target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            };

            if (!_doAfter.TryStartDoAfter(doAfter))
                return;

            Incant(ev.Performer, ev);
            return;
        }

        _popup.PopupEntity(Loc.GetString("blood-cult-twisted-construction-invalid"), ev.Performer, ev.Performer);
    }

    private void OnTwistedConstructionDoAfter(Entity<BloodCultSpellsHolderComponent> cultist, ref BloodCultTwistedConstructionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target || TerminatingOrDeleted(target))
            return;

        args.Handled = true;

        var replacement = Spawn(args.Replacement, _transform.GetMapCoordinates(target));
        if (TryComp<StackComponent>(target, out var stack) && TryComp<StackComponent>(replacement, out var newStack))
            _stack.SetCount((replacement, newStack), stack.Count);

        if (_mind.TryGetMind(target, out var mindId, out _))
            _mind.TransferTo(mindId, replacement);

        QueueDel(target);
        SpendSpell<BloodCultTwistedConstructionEvent>(cultist);
    }

    private bool Matches(TwistedConstructionRecipe recipe, EntityUid target)
    {
        if (_whitelist.IsWhitelistPass(recipe.Whitelist, target))
            return true;

        if (recipe.StackType is { } stackType && TryComp<StackComponent>(target, out var stack) && stack.StackTypeId == stackType)
            return true;

        return MetaData(target).EntityPrototype is { } proto && recipe.Prototypes.Contains(proto.ID);
    }

    /// <summary>
    /// Spends a charge of the first prepared spell of the given kind, for spells that only resolve after a do-after.
    /// </summary>
    private void SpendSpell<T>(Entity<BloodCultSpellsHolderComponent> cultist) where T : BaseActionEvent
    {
        foreach (var spell in cultist.Comp.SelectedSpells)
        {
            if (_actions.GetEvent(spell) is not T)
                continue;

            if (TryComp<LimitedChargesComponent>(spell, out var charges))
            {
                _charges.TryUseCharge((spell, charges));
                if (!_charges.IsEmpty((spell, charges)))
                    return;
            }

            cultist.Comp.SelectedSpells.Remove(spell);
            _actions.RemoveAction(cultist.Owner, spell);
            return;
        }
    }

    #endregion
}
