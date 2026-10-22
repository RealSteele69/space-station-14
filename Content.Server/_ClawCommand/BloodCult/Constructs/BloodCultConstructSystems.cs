using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared._ClawCommand.BloodCult.Roles;
using Content.Shared._ClawCommand.BloodCult.Systems;
using Content.Shared.Actions;
using Content.Shared.Bible.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Eye;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Coordinates.Helpers;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.BloodCult.Constructs;

/// <summary>
/// Soul shards hold the souls of the sacrificed. A filled shard can be let out as a shade,
/// or put into a construct shell to become a construct.
/// </summary>
[RegisterComponent]
public sealed partial class SoulShardComponent : Component
{
    [DataField]
    public bool IsBlessed;

    [DataField]
    public Color BlessedLightColor = Color.LightCyan;

    [DataField]
    public EntProtoId ShadeProto = "ShadeCult";

    [DataField]
    public EntProtoId PurifiedShadeProto = "ShadeHoly";

    [ViewVariables]
    public EntityUid? ShadeUid;
}

/// <summary>
/// An empty construct body. Put a filled soul shard in it to pick a construct form.
/// </summary>
[RegisterComponent]
public sealed partial class ConstructShellComponent : Component
{
    [DataField(required: true)]
    public ItemSlot ShardSlot = new();

    [DataField]
    public string ShardSlotId = "Shard";

    [DataField]
    public List<EntProtoId> Constructs = new()
    {
        "ConstructJuggernaut",
        "ConstructArtificer",
        "ConstructWraith",
    };

    [DataField]
    public List<EntProtoId> PurifiedConstructs = new()
    {
        "ConstructJuggernautHoly",
        "ConstructArtificerHoly",
        "ConstructWraithHoly",
    };
}

/// <summary>
/// Construct lifecycle, construct abilities and the wraith's phase shift.
/// </summary>
public sealed partial class BloodCultConstructSystem : SharedBloodCultPhaseShiftSystem
{
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VisibilitySystem _visibility = default!;
    [Dependency] private SharedRoleSystem _role = default!;

    private static readonly EntProtoId TileSpawnEffect = "CultTileSpawnEffect";
    private static readonly EntProtoId ConstructMindRole = "MindRoleBloodCultist";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultConstructComponent, MapInitEvent>(OnConstructMapInit);
        SubscribeLocalEvent<BloodCultConstructComponent, ComponentShutdown>(OnConstructShutdown);
        SubscribeLocalEvent<BloodCultConstructComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BloodCultConstructComponent, MindAddedMessage>(OnConstructMindAdded);

        SubscribeLocalEvent<BloodCultPlaceTileEntityEvent>(OnPlaceTileEntity);
        SubscribeLocalEvent<BloodCultPhaseShiftEvent>(OnPhaseShift);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = Timing.CurTime;

        var constructs = EntityQueryEnumerator<BloodCultConstructComponent>();
        while (constructs.MoveNext(out var uid, out var construct))
        {
            if (construct.TransformEnd is not { } end || end > now)
                continue;

            construct.TransformEnd = null;
            _appearance.SetData(uid, ConstructVisualsState.Transforming, false);
        }

        var shifted = EntityQueryEnumerator<BloodCultPhaseShiftedComponent>();
        while (shifted.MoveNext(out var uid, out var shift))
        {
            if (shift.EndTime <= now)
                RemCompDeferred(uid, shift);
        }
    }

    #region Constructs

    private void OnConstructMapInit(Entity<BloodCultConstructComponent> construct, ref MapInitEvent args)
    {
        foreach (var actionId in construct.Comp.Actions)
        {
            if (_actions.AddAction(construct, actionId) is { } action)
                construct.Comp.ActionEntities.Add(action);
        }

        _appearance.SetData(construct, ConstructVisualsState.Transforming, true);
        construct.Comp.TransformEnd = Timing.CurTime + construct.Comp.TransformDelay;
    }

    private void OnConstructShutdown(Entity<BloodCultConstructComponent> construct, ref ComponentShutdown args)
    {
        foreach (var action in construct.Comp.ActionEntities)
        {
            _actions.RemoveAction(action);
        }
    }

    /// <summary>
    /// A soul bound into a cult construct serves the cult, so its mind carries the cult role
    /// (and shows up as an antagonist to admins).
    /// </summary>
    private void OnConstructMindAdded(Entity<BloodCultConstructComponent> construct, ref MindAddedMessage args)
    {
        if (construct.Comp.Holy || _role.MindHasRole<BloodCultRoleComponent>(args.Mind.Owner))
            return;

        _role.MindAddRole(args.Mind.Owner, ConstructMindRole, args.Mind.Comp, true);
    }

    private void OnMobStateChanged(Entity<BloodCultConstructComponent> construct, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        Spawn(construct.Comp.SpawnOnDeath, Transform(construct).Coordinates);
        QueueDel(construct);
    }

    private void OnPlaceTileEntity(BloodCultPlaceTileEntityEvent args)
    {
        if (args.Handled)
            return;

        if (args.TileId is { } tileId)
        {
            if (_transform.GetGrid(args.Target) is not { } grid || !TryComp<MapGridComponent>(grid, out var mapGrid) ||
                !_tileDef.TryGetDefinition(tileId, out var tileDef))
                return;

            _map.SetTile(grid, mapGrid, args.Target, new Tile(tileDef.TileId));
            Spawn(TileSpawnEffect, args.Target);
        }

        if (args.SpawnedEntity is { } entity)
            Spawn(entity, args.Target.SnapToGrid(EntityManager));

        if (args.Audio is { } audio)
            _audio.PlayPvs(audio, args.Target);

        args.Handled = true;
    }

    #endregion

    #region Phase shift

    private void OnPhaseShift(BloodCultPhaseShiftEvent args)
    {
        if (args.Handled)
            return;

        PhaseShift(args.Performer, args.Duration);
        args.Handled = true;
    }

    protected override void OnStartup(Entity<BloodCultPhaseShiftedComponent> ent, ref ComponentStartup args)
    {
        base.OnStartup(ent, ref args);

        // Only the dead see a shifted wraith.
        var visibility = EnsureComp<VisibilityComponent>(ent);
        _visibility.AddLayer((ent, visibility), (ushort) VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer((ent, visibility), (ushort) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(ent, visibility);
    }

    protected override void OnShutdown(Entity<BloodCultPhaseShiftedComponent> ent, ref ComponentShutdown args)
    {
        base.OnShutdown(ent, ref args);

        if (TerminatingOrDeleted(ent) || !TryComp<VisibilityComponent>(ent, out var visibility))
            return;

        _visibility.RemoveLayer((ent, visibility), (ushort) VisibilityFlags.Ghost, false);
        _visibility.AddLayer((ent, visibility), (ushort) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(ent, visibility);
    }

    #endregion
}

public sealed partial class SoulShardSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BloodCultItemSystem _cultItem = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoulShardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SoulShardComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SoulShardComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<SoulShardComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<SoulShardComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnMapInit(Entity<SoulShardComponent> shard, ref MapInitEvent args)
    {
        if (shard.Comp.IsBlessed)
            Bless(shard, false);
    }

    private void OnActivate(Entity<SoulShardComponent> shard, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        // A cult shard answers to the cult; a blessed one to anyone.
        if (!shard.Comp.IsBlessed && !_cultItem.IsCultMember(args.User))
            return;

        args.Handled = true;

        if (shard.Comp.ShadeUid is { } shade && !TerminatingOrDeleted(shade))
        {
            RecallShade(shard, shade);
            return;
        }

        if (!_mind.TryGetMind(shard, out var mindId, out _))
        {
            _popup.PopupEntity(Loc.GetString("soul-shard-try-insert-no-soul"), shard, args.User);
            return;
        }

        var shadeUid = Spawn(shard.Comp.IsBlessed ? shard.Comp.PurifiedShadeProto : shard.Comp.ShadeProto, _transform.GetMapCoordinates(shard));
        shard.Comp.ShadeUid = shadeUid;
        _mind.TransferTo(mindId, shadeUid);
        _mind.UnVisit(mindId);
    }

    /// <summary>
    /// Pulls the shade back into its shard, which mends it.
    /// </summary>
    private void RecallShade(Entity<SoulShardComponent> shard, EntityUid shade)
    {
        if (_mind.TryGetMind(shade, out var mindId, out _))
        {
            _mind.TransferTo(mindId, shard);
            _mind.UnVisit(mindId);
        }

        shard.Comp.ShadeUid = null;
        QueueDel(shade);
    }

    private void OnInteractUsing(Entity<SoulShardComponent> shard, ref InteractUsingEvent args)
    {
        if (args.Handled || shard.Comp.IsBlessed || !TryComp<BibleComponent>(args.Used, out var bible) ||
            !HasComp<BibleUserComponent>(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("soul-shard-blessed"), args.User, args.User);
        _audio.PlayPvs(bible.HealSound, args.User);
        Bless(shard, true);
    }

    private void Bless(Entity<SoulShardComponent> shard, bool deconvert)
    {
        shard.Comp.IsBlessed = true;
        _appearance.SetData(shard, SoulShardVisualState.Blessed, true);
        _light.SetColor(shard, shard.Comp.BlessedLightColor);

        if (deconvert && TryComp<BloodCultItemComponent>(shard, out var cultItem))
            cultItem.AllowUseToEveryone = true;
    }

    private void OnMindAdded(Entity<SoulShardComponent> shard, ref MindAddedMessage args)
    {
        // A trapped soul has no business keeping its old traitor objectives.
        _role.MindRemoveRole<TraitorRoleComponent>(args.Mind.Owner);
        UpdateGlow(shard, true);
    }

    private void OnMindRemoved(Entity<SoulShardComponent> shard, ref MindRemovedMessage args)
    {
        UpdateGlow(shard, false);
    }

    private void UpdateGlow(Entity<SoulShardComponent> shard, bool state)
    {
        _appearance.SetData(shard, SoulShardVisualState.HasMind, state);
        _light.SetEnabled(shard, state);
    }
}

public sealed partial class ConstructShellSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier FormSound = new SoundPathSpecifier("/Audio/_ClawCommand/Heretic/constructform.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConstructShellComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ConstructShellComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ConstructShellComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ConstructShellComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ConstructShellComponent, BloodCultOptionSelectedMessage>(OnFormSelected);
    }

    private void OnInit(Entity<ConstructShellComponent> shell, ref ComponentInit args)
    {
        _slots.AddItemSlot(shell.Owner, shell.Comp.ShardSlotId, shell.Comp.ShardSlot);
    }

    private void OnRemove(Entity<ConstructShellComponent> shell, ref ComponentRemove args)
    {
        _slots.RemoveItemSlot(shell.Owner, shell.Comp.ShardSlot);
    }

    private void OnInsertAttempt(Entity<ConstructShellComponent> shell, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != shell.Comp.ShardSlotId || !HasComp<SoulShardComponent>(args.EntityUid))
            return;

        if (!_mind.TryGetMind(args.EntityUid, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("soul-shard-try-insert-no-soul"), shell);
            args.Cancel();
        }
    }

    private void OnInserted(Entity<ConstructShellComponent> shell, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != shell.Comp.ShardSlotId || !TryComp<SoulShardComponent>(args.Entity, out var shard) ||
            !_mind.TryGetMind(args.Entity, out _, out var mind) || mind.CurrentEntity is not { } current)
            return;

        _slots.SetLock(shell.Owner, shell.Comp.ShardSlotId, true);

        // The soul inside the shard picks its own body.
        var entries = new List<BloodCultRadialEntry>();
        foreach (var form in shard.IsBlessed ? shell.Comp.PurifiedConstructs : shell.Comp.Constructs)
        {
            if (_proto.TryIndex(form, out var proto))
                entries.Add(new BloodCultRadialEntry(form, proto.Name, form));
        }

        _ui.SetUiState(shell.Owner, BloodCultUiKey.Shell, new BloodCultRadialState(entries));
        _ui.OpenUi(shell.Owner, BloodCultUiKey.Shell, current);
    }

    private void OnFormSelected(Entity<ConstructShellComponent> shell, ref BloodCultOptionSelectedMessage args)
    {
        if (!args.UiKey.Equals(BloodCultUiKey.Shell) ||
            _slots.GetItemOrNull(shell.Owner, shell.Comp.ShardSlotId) is not { } shardUid ||
            !TryComp<SoulShardComponent>(shardUid, out var shard) ||
            !_mind.TryGetMind(shardUid, out var mindId, out var mind) || mind.CurrentEntity != args.Actor)
            return;

        var forms = shard.IsBlessed ? shell.Comp.PurifiedConstructs : shell.Comp.Constructs;
        var form = new EntProtoId(args.Id);
        if (!forms.Contains(form))
            return;

        _ui.CloseUi(shell.Owner, BloodCultUiKey.Shell);

        var construct = Spawn(form, _transform.GetMapCoordinates(shell));
        _mind.TransferTo(mindId, construct);
        _audio.PlayPvs(FormSound, construct);
        QueueDel(shell);
    }
}
