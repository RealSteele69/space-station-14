using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Actions;
using Content.Shared.Blocking;
using Content.Shared.Ghost.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Toggleable;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._ClawCommand.BloodCult.Systems;

/// <summary>
/// Keeps cult gear in cult hands. Outsiders who try to use it are knocked down and drop it.
/// </summary>
public sealed partial class BloodCultItemSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultItemComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BloodCultItemComponent, UseInHandEvent>(OnUseInHand, before: [typeof(ActivatableUISystem)]);
        SubscribeLocalEvent<BloodCultItemComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<BloodCultItemComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<BloodCultItemComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<BloodCultItemComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<BloodCultItemComponent, ToggleActionEvent>(OnBlockToggle, before: [typeof(BlockingSystem)]);
    }

    /// <summary>
    /// Cultists, cult constructs and ghosts may handle cult items.
    /// </summary>
    public bool IsCultMember(EntityUid? uid)
    {
        if (uid is not { } ent)
            return false;

        if (HasComp<BloodCultistComponent>(ent) || HasComp<GhostComponent>(ent))
            return true;

        return TryComp<BloodCultConstructComponent>(ent, out var construct) && !construct.Holy;
    }

    private bool CanUse(Entity<BloodCultItemComponent> item, EntityUid user)
    {
        return item.Comp.AllowUseToEveryone || IsCultMember(user);
    }

    private void OnActivate(Entity<BloodCultItemComponent> item, ref ActivateInWorldEvent args)
    {
        if (args.Handled || CanUse(item, args.User))
            return;

        args.Handled = true;
        KnockdownAndDrop(item, args.User, "cult-item-component-generic");
    }

    private void OnUseInHand(Entity<BloodCultItemComponent> item, ref UseInHandEvent args)
    {
        if (args.Handled || CanUse(item, args.User))
            return;

        // Outsiders may still pull a cult weapon out of themselves; they get knocked down when they try to use it afterwards.
        if (TryComp<EmbeddableProjectileComponent>(item, out var embeddable) && embeddable.EmbeddedIntoUid != null)
            return;

        args.Handled = true;
        KnockdownAndDrop(item, args.User, "cult-item-component-generic");
    }

    private void OnUiOpenAttempt(Entity<BloodCultItemComponent> item, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!CanUse(item, args.User))
            args.Cancel();
    }

    private void OnThrowAttempt(Entity<BloodCultItemComponent> item, ref ThrowItemAttemptEvent args)
    {
        if (args.Cancelled || CanUse(item, args.User))
            return;

        args.Cancelled = true;
        KnockdownAndDrop(item, args.User, "cult-item-component-throw-fail");
    }

    private void OnEquipAttempt(Entity<BloodCultItemComponent> item, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled || CanUse(item, args.EquipTarget))
            return;

        args.Cancel();
        KnockdownAndDrop(item, args.User, "cult-item-component-equip-fail");
    }

    private void OnMeleeAttempt(Entity<BloodCultItemComponent> item, ref AttemptMeleeEvent args)
    {
        if (args.Cancelled || CanUse(item, args.User))
            return;

        args.Cancelled = true;
        KnockdownAndDrop(item, args.User, "cult-item-component-attack-fail");
    }

    private void OnBlockToggle(Entity<BloodCultItemComponent> item, ref ToggleActionEvent args)
    {
        if (args.Handled || CanUse(item, args.Performer))
            return;

        args.Handled = true;
        KnockdownAndDrop(item, args.Performer, "cult-item-component-block-fail");
    }

    private void KnockdownAndDrop(Entity<BloodCultItemComponent> item, EntityUid user, string message)
    {
        _popup.PopupPredicted(Loc.GetString(message), item, user);
        _stun.TryKnockdown(user, item.Comp.KnockdownDuration, true);
        _hands.TryDrop(user, item.Owner);
    }
}
