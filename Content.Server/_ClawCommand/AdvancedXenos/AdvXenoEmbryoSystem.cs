using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared._ClawCommand.AdvancedXenos.EntityEffects;
using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.EntityEffects;
using Content.Shared.Gibbing;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Standing;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.AdvancedXenos;

/// <summary>
/// Claw Command - the alien embryo: six stages of growing symptoms, then a chestburster.
/// It would normally be cut out with surgery; this fork has none, so a scalpel on a patient lying down does the job.
/// </summary>
public sealed partial class AdvXenoEmbryoSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    private static readonly List<EntProtoId> XenomorphMindRoles = new() { "MindRoleAdvancedXenomorph" };

    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private const string SlicingQuality = "Slicing";
    private const string SneezeEmote = "Sneeze";
    private const string CoughEmote = "Cough";

    private static readonly EntProtoId BurstStanding = "AdvXenoChestburstEffect";
    private static readonly EntProtoId BurstLying = "AdvXenoChestburstEffectLying";
    private static readonly EntProtoId EmbryoItem = "AdvXenoEmbryoRemains";
    private static readonly SoundSpecifier BurstSound = new SoundPathSpecifier("/Audio/_ClawCommand/AdvancedXenos/Voice/hiss5.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdvXenoEmbryoComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AdvXenoEmbryoComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<InventoryComponent, GetVerbsEvent<UtilityVerb>>(OnGetSurgeryVerbs);
        SubscribeLocalEvent<InventoryComponent, AdvXenoEmbryoSurgeryDoAfterEvent>(OnSurgeryDoAfter);
    }

    private void OnMapInit(Entity<AdvXenoEmbryoComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + ent.Comp.TickInterval;
    }

    private void OnStartup(Entity<AdvXenoEmbryoComponent> ent, ref ComponentStartup args)
    {
        // added at runtime by a facehugger, so MapInit already passed
        if (ent.Comp.NextTick == TimeSpan.Zero)
            ent.Comp.NextTick = _timing.CurTime + ent.Comp.TickInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<AdvXenoEmbryoComponent>();
        while (query.MoveNext(out var uid, out var embryo))
        {
            if (embryo.BurstAt is { } burstAt)
            {
                if (now >= burstAt)
                    FinishBurst((uid, embryo));
                continue;
            }

            if (now < embryo.NextTick)
                continue;

            embryo.NextTick = now + embryo.TickInterval;
            Tick((uid, embryo));
        }
    }

    // /obj/item/alien_embryo/process
    private void Tick(Entity<AdvXenoEmbryoComponent> ent)
    {
        var host = ent.Owner;
        if (ent.Comp.Stage < 5 && _random.Prob(0.02f))
        {
            ent.Comp.Stage++;
            Dirty(ent);
        }

        switch (ent.Comp.Stage)
        {
            case 2:
            case 3:
                if (_random.Prob(0.01f))
                    _chat.TryEmoteWithChat(host, SneezeEmote, ignoreActionBlocker: true);
                if (_random.Prob(0.01f))
                    _chat.TryEmoteWithChat(host, CoughEmote, ignoreActionBlocker: true);
                if (_random.Prob(0.01f))
                    _popup.PopupEntity(Loc.GetString("adv-xeno-embryo-throat"), host, host, PopupType.SmallCaution);
                if (_random.Prob(0.01f))
                    _popup.PopupEntity(Loc.GetString("adv-xeno-embryo-mucous"), host, host, PopupType.SmallCaution);
                break;

            case 4:
                if (_random.Prob(0.01f))
                    _chat.TryEmoteWithChat(host, SneezeEmote, ignoreActionBlocker: true);
                if (_random.Prob(0.01f))
                    _chat.TryEmoteWithChat(host, CoughEmote, ignoreActionBlocker: true);
                if (_random.Prob(0.02f))
                {
                    _popup.PopupEntity(Loc.GetString("adv-xeno-embryo-muscles"), host, host, PopupType.SmallCaution);
                    if (_random.Prob(0.2f))
                        _damage.TryChangeDamage(host, new DamageSpecifier(_proto.Index(Blunt), 1), ignoreResistances: true, interruptsDoAfters: false);
                }
                if (_random.Prob(0.02f))
                {
                    _popup.PopupEntity(Loc.GetString("adv-xeno-embryo-stomach"), host, host, PopupType.SmallCaution);
                    if (_random.Prob(0.2f))
                        _damage.TryChangeDamage(host, new DamageSpecifier(_proto.Index(Poison), 1), ignoreResistances: true, interruptsDoAfters: false);
                }
                break;

            case 5:
                _popup.PopupEntity(Loc.GetString("adv-xeno-embryo-tearing"), host, host, PopupType.LargeCaution);
                _damage.TryChangeDamage(host, new DamageSpecifier(_proto.Index(Poison), 10), ignoreResistances: true, interruptsDoAfters: false);
                if (_random.Prob(0.5f))
                    StartBurst(ent);
                break;
        }
    }

    // AttemptGrow: the burst overlay, then a larva and a gibbed host six ticks later.
    private void StartBurst(Entity<AdvXenoEmbryoComponent> ent)
    {
        var effect = Spawn(_standing.IsDown(ent.Owner) ? BurstLying : BurstStanding, Transform(ent).Coordinates);
        _transform.SetParent(effect, ent.Owner);
        ent.Comp.BurstAt = _timing.CurTime + TimeSpan.FromSeconds(0.6);
    }

    private void FinishBurst(Entity<AdvXenoEmbryoComponent> ent)
    {
        var coords = _transform.GetMapCoordinates(ent);
        var hasMind = _mind.TryGetMind(ent.Owner, out var mindId, out var mind);

        var larva = Spawn(ent.Comp.Larva, coords);
        _audio.PlayPvs(BurstSound, larva, AudioParams.Default.WithVolume(4f));
        _adminLog.Add(LogType.Gib, LogImpact.High, $"{ToPrettyString(ent):target} burst open, releasing alien larva {ToPrettyString(larva)}");

        // the host was an actual player, not an SSD body or an NPC: they become the larva at once,
        // rather than leaving it up as a ghost role for someone else to take.
        if (hasMind)
        {
            RemComp<GhostRoleComponent>(larva);
            RemComp<GhostTakeoverAvailableComponent>(larva);
            _mind.TransferTo(mindId, larva, mind: mind);
            _role.MindAddRoles(mindId, XenomorphMindRoles, mind);
            _popup.PopupEntity(Loc.GetString("adv-xeno-embryo-burst-takeover"), larva, larva, PopupType.LargeCaution);
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):target}'s mind took over the burst larva {ToPrettyString(larva)}");
        }

        RemComp<AdvXenoEmbryoComponent>(ent);
        _gibbing.Gib(ent);
    }

    #region Extraction

    private void OnGetSurgeryVerbs(Entity<InventoryComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } tool || args.User == ent.Owner)
            return;
        if (!_tool.HasQuality(tool, SlicingQuality))
            return;
        if (!_standing.IsDown(ent.Owner) && !_buckle.IsBuckled(ent))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("adv-xeno-surgery-verb"),
            Message = Loc.GetString("adv-xeno-surgery-verb-desc"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_ClawCommand/Objects/AdvancedXenos/embryo.rsi"), "embryo"),
            Act = () => StartSurgery(user, target, tool),
        });
    }

    private void StartSurgery(EntityUid user, EntityUid target, EntityUid tool)
    {
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(10), new AdvXenoEmbryoSurgeryDoAfterEvent(), target, target, tool)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        });

        if (!started)
            return;

        _popup.PopupEntity(Loc.GetString("adv-xeno-surgery-start", ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
            target, PopupType.MediumCaution);
    }

    private void OnSurgeryDoAfter(Entity<InventoryComponent> ent, ref AdvXenoEmbryoSurgeryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var target = ent.Owner;
        _damage.TryChangeDamage(target, new DamageSpecifier(_proto.Index(Slash), 15), origin: args.User);

        if (!TryComp<AdvXenoEmbryoComponent>(target, out var embryo) || embryo.BurstAt != null)
        {
            _popup.PopupEntity(Loc.GetString("adv-xeno-surgery-nothing"), target, args.User);
            return;
        }

        RemComp<AdvXenoEmbryoComponent>(target);
        Spawn(EmbryoItem, Transform(target).Coordinates);
        _popup.PopupEntity(Loc.GetString("adv-xeno-surgery-success", ("user", Identity.Entity(args.User, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
            target, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):user} removed an alien embryo from {ToPrettyString(target):target}");
    }

    #endregion
}

/// <summary>
/// Chemistry's answer to the embryo: a parasiticide cures it before it starts bursting.
/// </summary>
public sealed partial class CureAdvXenoEmbryoEntityEffectSystem : EntityEffectSystem<AdvXenoEmbryoComponent, CureAdvXenoEmbryo>
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    protected override void Effect(Entity<AdvXenoEmbryoComponent> entity, ref EntityEffectEvent<CureAdvXenoEmbryo> args)
    {
        if (entity.Comp.BurstAt != null)
            return;

        RemComp<AdvXenoEmbryoComponent>(entity);
        _popup.PopupEntity(Loc.GetString("adv-xeno-embryo-cured"), entity, entity, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"An alien embryo in {ToPrettyString(entity):target} was cured by chemistry");
    }
}
