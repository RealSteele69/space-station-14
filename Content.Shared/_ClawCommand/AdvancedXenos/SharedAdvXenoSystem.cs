using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Doors;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - the predicted half of the advanced xenomorphs: who may pick up live facehuggers,
/// who may open resin doors, who may use nests, and the "no fine manipulation" gun block.
/// Everything with timers or spawning lives in the server systems.
/// </summary>
public sealed partial class SharedAdvXenoSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoFacehuggerComponent, GettingPickedUpAttemptEvent>(OnHuggerPickupAttempt);
        SubscribeLocalEvent<AdvXenoResinDoorComponent, BeforeDoorOpenedEvent>(OnResinDoorOpen);
        SubscribeLocalEvent<AdvXenoHiveComponent, ShotAttemptedEvent>(OnShotAttempted);

        SubscribeLocalEvent<AdvXenoNestComponent, StrapAttemptEvent>(OnNestStrapAttempt);
        SubscribeLocalEvent<AdvXenoNestComponent, StrappedEvent>(OnNestStrapped);
        SubscribeLocalEvent<AdvXenoNestComponent, UnstrapAttemptEvent>(OnNestUnstrapAttempt);
        SubscribeLocalEvent<AdvXenoNestComponent, UnstrappedEvent>(OnNestUnstrapped);
        SubscribeLocalEvent<BuckleComponent, AdvXenoNestResistDoAfterEvent>(OnNestResistDoAfter);

        SubscribeLocalEvent<AdvXenoHiderComponent, AdvXenoHideActionEvent>(OnHide);

        SubscribeLocalEvent<AdvXenoDevouredComponent, AttackAttemptEvent>(OnDevouredAttackAttempt);
    }

    public bool IsHive(EntityUid? uid)
    {
        return uid != null && HasComp<AdvXenoHiveComponent>(uid);
    }

    // attack_hand on a conscious, fertile hugger latches it onto whoever reached for it.
    // Aliens (hive node) handle them freely.
    private void OnHuggerPickupAttempt(Entity<AdvXenoFacehuggerComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.State != AdvXenoHuggerState.Active || ent.Comp.Sterile || IsHive(args.User))
            return;

        args.Cancel();
        if (_net.IsServer && args.ShowPopup)
            ent.Comp.PendingAttach = args.User;
    }

    // /datum/prototype/material/resin/can_open_material_door
    private void OnResinDoorOpen(Entity<AdvXenoResinDoorComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (!IsHive(args.User))
            args.Cancel();
    }

    // has_fine_manipulation = 0
    private void OnShotAttempted(Entity<AdvXenoHiveComponent> ent, ref ShotAttemptedEvent args)
    {
        _popup.PopupEntity(Loc.GetString("adv-xeno-no-guns"), ent, ent);
        args.Cancel();
    }

    #region Nest

    // /obj/structure/bed/nest/user_buckle_mob: only an alien may secure someone, never another alien, never themselves.
    private void OnNestStrapAttempt(Entity<AdvXenoNestComponent> ent, ref StrapAttemptEvent args)
    {
        var victim = args.Buckle.Owner;
        if (args.User is not { } user || user == victim || !IsHive(user) || IsHive(victim))
        {
            if (args.Popup && args.User is { } u)
                _popup.PopupEntity(Loc.GetString("adv-xeno-nest-cannot"), ent, u);
            args.Cancelled = true;
        }
    }

    private void OnNestStrapped(Entity<AdvXenoNestComponent> ent, ref StrappedEvent args)
    {
        if (_net.IsClient)
            return;

        var victim = args.Buckle.Owner;
        _popup.PopupEntity(Loc.GetString("adv-xeno-nest-secured", ("nest", ent.Owner), ("victim", Identity.Entity(victim, EntityManager))),
            victim, Filter.PvsExcept(victim), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("adv-xeno-nest-secured-self", ("nest", ent.Owner)), victim, victim, PopupType.LargeCaution);
    }

    // mob_resist_buckle: the captive struggles for NEST_RESIST_TIME. Anyone else just pulls them free.
    private void OnNestUnstrapAttempt(Entity<AdvXenoNestComponent> ent, ref UnstrapAttemptEvent args)
    {
        var victim = args.Buckle.Owner;
        if (args.User != victim || IsHive(victim))
            return;

        args.Cancelled = true;
        if (!args.Popup)
            return;

        var doAfter = new DoAfterArgs(EntityManager, victim, ent.Comp.ResistTime, new AdvXenoNestResistDoAfterEvent(), victim, used: ent)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            RequireCanInteract = false,
            NeedHand = false,
            BlockDuplicate = true,
            CancelDuplicate = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-nest-struggle", ("victim", Identity.Entity(victim, EntityManager))),
                victim, Filter.PvsExcept(victim), true, PopupType.Small);
            _popup.PopupEntity(Loc.GetString("adv-xeno-nest-struggle-self"), victim, victim, PopupType.Medium);
        }
    }

    private void OnNestResistDoAfter(Entity<BuckleComponent> ent, ref AdvXenoNestResistDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-nest-fail"), ent, ent);
            return;
        }

        if (ent.Comp.BuckledTo is not { } strap || !HasComp<AdvXenoNestComponent>(strap))
            return;

        // Bypasses the attempt events: the struggle already paid for it.
        _buckle.Unbuckle(ent.AsNullable(), null);
    }

    private void OnNestUnstrapped(Entity<AdvXenoNestComponent> ent, ref UnstrappedEvent args)
    {
        if (_net.IsClient)
            return;

        var victim = args.Buckle.Owner;
        _popup.PopupEntity(Loc.GetString("adv-xeno-nest-free", ("nest", ent.Owner), ("victim", Identity.Entity(victim, EntityManager))),
            victim, PopupType.Small);
    }

    #endregion

    // Swallowed prey (/mob/living/carbon/human/proc/devour): no struggling or fighting your way out,
    // only regurgitation or the xeno's death frees you.
    private void OnDevouredAttackAttempt(Entity<AdvXenoDevouredComponent> ent, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    // /mob/living/proc/hide
    private void OnHide(Entity<AdvXenoHiderComponent> ent, ref AdvXenoHideActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Hidden = !ent.Comp.Hidden;
        Dirty(ent);
        _appearance.SetData(ent, AdvXenoLarvaVisuals.Hidden, ent.Comp.Hidden);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Hidden ? "adv-xeno-hide-on" : "adv-xeno-hide-off"), ent, ent);
    }
}
