using System.Linq;
using Content.Shared._ClawCommand.VentCrawl;
using Content.Shared.Atmos.Components;
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._ClawCommand.VentCrawl;

/// <summary>
/// Claw Command - what a vent crawler sees: every pipe and atmos fitting around you is pulled out from under
/// the floor and drawn fullbright, above the lighting, so the duct layout, its junctions and the vents you can climb out of stand out even in a pitch-black maintenance
/// tunnel. You are drawn at your spot in the pipe on top of it all.
/// </summary>
public sealed partial class VentCrawlVisionSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float Range = 14f;
    private const string Unshaded = "unshaded";

    /// <summary>Pipes we have revealed, with each layer's shader before we made it unshaded.</summary>
    private readonly Dictionary<EntityUid, List<(ProtoId<ShaderPrototype>? Proto, ShaderInstance? Shader)>> _revealed = new();
    private readonly HashSet<Entity<SubFloorHideComponent>> _inRange = new();
    private float _accumulator;

    private VentCrawlOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new VentCrawlOverlay();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
        UnrevealAll();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var player = _player.LocalEntity;
        if (player == null || !HasComp<VentCrawlingComponent>(player))
        {
            if (_overlays.HasOverlay<VentCrawlOverlay>())
                _overlays.RemoveOverlay(_overlay);
            if (_revealed.Count > 0)
                UnrevealAll();
            return;
        }

        if (!_overlays.HasOverlay<VentCrawlOverlay>())
            _overlays.AddOverlay(_overlay);

        // server states reset client-only appearance data, so keep insisting
        foreach (var uid in _revealed.Keys)
        {
            if (!_appearance.TryGetData<bool>(uid, SubFloorVisuals.ScannerRevealed, out var revealed) || !revealed)
                _appearance.SetData(uid, SubFloorVisuals.ScannerRevealed, true);
        }

        _accumulator += frameTime;
        if (_accumulator < 0.25f)
            return;
        _accumulator = 0f;

        var xform = Transform(player.Value);
        _inRange.Clear();
        _lookup.GetEntitiesInRange(xform.MapID, _transform.GetWorldPosition(xform), Range, _inRange,
            LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Approximate);

        var keep = new HashSet<EntityUid>();
        foreach (var (uid, _) in _inRange)
        {
            if (!HasComp<PipeAppearanceComponent>(uid) && !HasComp<AtmosPipeLayersComponent>(uid))
                continue;

            keep.Add(uid);
            if (!_revealed.ContainsKey(uid))
                Reveal(uid);
        }

        foreach (var uid in new List<EntityUid>(_revealed.Keys))
        {
            if (!keep.Contains(uid))
                Unreveal(uid);
        }
    }

    private void Reveal(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var saved = new List<(ProtoId<ShaderPrototype>?, ShaderInstance?)>();
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            var layer = sprite[i];
            if (layer is not SpriteComponent.Layer concrete)
            {
                saved.Add((null, null));
                continue;
            }

            saved.Add((concrete.ShaderPrototype, concrete.Shader));
#pragma warning disable CS0618
            sprite.LayerSetShader(i, Unshaded);
#pragma warning restore CS0618
        }

        _revealed[uid] = saved;
        _appearance.SetData(uid, SubFloorVisuals.ScannerRevealed, true);
    }

    private void Unreveal(EntityUid uid)
    {
        if (!_revealed.Remove(uid, out var saved) || TerminatingOrDeleted(uid))
            return;

        _appearance.SetData(uid, SubFloorVisuals.ScannerRevealed, false);
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        for (var i = 0; i < saved.Count && i < sprite.AllLayers.Count(); i++)
        {
            var (proto, shader) = saved[i];
#pragma warning disable CS0618
            sprite.LayerSetShader(i, shader, proto?.Id);
#pragma warning restore CS0618
        }
    }

    private void UnrevealAll()
    {
        foreach (var uid in new List<EntityUid>(_revealed.Keys))
            Unreveal(uid);
    }
}

/// <summary>
/// The centre marker: the crawler, drawn at its place in the duct above the world's darkness and FOV.
/// </summary>
public sealed partial class VentCrawlOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public VentCrawlOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entMan.System<SpriteSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player
            || !_entMan.TryGetComponent<SpriteComponent>(player, out var sprite)
            || !_entMan.TryGetComponent<TransformComponent>(player, out var xform)
            || xform.MapID != args.MapId)
            return;

        var handle = args.WorldHandle;
        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        handle.Modulate = Color.White.WithAlpha(0.85f);
        _sprite.RenderSprite((player, sprite), handle, eyeRotation, rotation, position);
        handle.Modulate = Color.White;
    }
}
