using System.Collections.Generic;
using Content.Client.UserInterface.Systems.Sandbox;
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.SubFloor;

public sealed partial class SubFloorHideSystem : SharedSubFloorHideSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
#if DEBUG
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
#endif

    private bool _showAll;

    /// <summary>
    /// Claw Command - entities a client-local system (e.g. vent crawling) wants drawn as if revealed by a
    /// t-ray scanner, without going through <see cref="SharedAppearanceSystem.SetData"/>. That call writes into
    /// the networked <see cref="AppearanceComponent"/> dictionary, which the server fully overwrites on every
    /// state update for that component (pipes get those often, e.g. atmos recoloring) - wiping out a client-only
    /// flag almost as soon as it's set and causing the reveal to flicker or drop. This set lives only on the
    /// client, so it survives server state application.
    /// </summary>
    private readonly HashSet<EntityUid> _ventCrawlRevealed = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ShowAll
    {
        get => _showAll;
        set
        {
            if (_showAll == value) return;
            _showAll = value;
            _ui.GetUIController<SandboxUIController>().SetToggleSubfloors(value);

            var ev = new ShowSubfloorRequestEvent()
            {
                Value = value,
            };
            RaiseNetworkEvent(ev);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

#if DEBUG
        _sawmill = _logManager.GetSawmill("subfloor.hide");
#endif

        SubscribeLocalEvent<SubFloorHideComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeNetworkEvent<ShowSubfloorRequestEvent>(OnRequestReceived);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        // Vismask resets so need to reset this.
        ShowAll = false;
    }

    /// <summary>
    /// Claw Command - reveal (or stop revealing) an entity for a client-local reason, e.g. vent crawling.
    /// Unlike <see cref="SharedAppearanceSystem.SetData"/> with <see cref="SubFloorVisuals.ScannerRevealed"/>,
    /// this never touches the networked appearance data, so a server state update for this entity (which pipes
    /// get often) can't silently reset it.
    /// </summary>
    public void SetVentCrawlRevealed(EntityUid uid, bool revealed)
    {
        var changed = revealed ? _ventCrawlRevealed.Add(uid) : _ventCrawlRevealed.Remove(uid);
        if (!changed)
            return;

        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.QueueUpdate(uid, appearance);
        }
#if DEBUG
        else
        {
            _sawmill.Warning($"SetVentCrawlRevealed({ToPrettyString(uid)}, {revealed}): entity has no AppearanceComponent, reveal will never apply");
        }
#endif
    }

    private void OnRequestReceived(ShowSubfloorRequestEvent ev)
    {
        // When client receives request Queue an update on all vis.
        UpdateAll();
    }

    private void OnAppearanceChanged(EntityUid uid, SubFloorHideComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        _appearance.TryGetData<bool>(uid, SubFloorVisuals.Covered, out var covered, args.Component);
        _appearance.TryGetData<bool>(uid, SubFloorVisuals.ScannerRevealed, out var scannerRevealed, args.Component);
        var ventCrawlRevealed = _ventCrawlRevealed.Contains(uid);

        scannerRevealed &= !ShowAll; // no transparency for show-subfloor mode.
        // Claw Command - a vent crawler sees it the same way a t-ray scanner would.
        scannerRevealed |= ventCrawlRevealed && !ShowAll;

        var revealed = !covered || ShowAll || scannerRevealed;

#if DEBUG
        if (ventCrawlRevealed)
        {
            _sawmill.Info($"OnAppearanceChanged({ToPrettyString(uid)}): covered={covered} scannerRevealed={scannerRevealed} ventCrawlRevealed={ventCrawlRevealed} showAll={ShowAll} -> revealed={revealed}");
        }
#endif

        // set visibility & color of each layer
        foreach (var layer in args.Sprite.AllLayers)
        {
            // pipe connection visuals are updated AFTER this, and may re-hide some layers
            layer.Visible = revealed;
        }

        // Is there some layer that is always visible?
        var hasVisibleLayer = false;
        foreach (var layerKey in component.VisibleLayers)
        {
            if (!_sprite.LayerMapTryGet((uid, args.Sprite), layerKey, out var layerIndex, false))
                continue;

            var layer = args.Sprite[layerIndex];
            layer.Visible = true;
            layer.Color = layer.Color.WithAlpha(1f);
            hasVisibleLayer = true;
        }

        _sprite.SetVisible((uid, args.Sprite), hasVisibleLayer || revealed);

#if DEBUG
        if (ventCrawlRevealed)
        {
            _sawmill.Info($"OnAppearanceChanged({ToPrettyString(uid)}): after SetVisible, sprite.Visible={args.Sprite.Visible} drawDepth={args.Sprite.DrawDepth}");
        }
#endif

        if (ShowAll)
        {
            // Allows sandbox mode to make wires visible over other stuff.
            component.OriginalDrawDepth ??= args.Sprite.DrawDepth;
            var drawDepthDifference = Shared.DrawDepth.DrawDepth.ThickPipe - (Shared.DrawDepth.DrawDepth.Overdoors + 1);
            _sprite.SetDrawDepth((uid, args.Sprite), args.Sprite.DrawDepth - drawDepthDifference);
        }
        else if (scannerRevealed)
        {
            // Allows a t-ray to show wires/pipes above carpets/puddles.
            if (component.OriginalDrawDepth is not null)
                return;
            component.OriginalDrawDepth = args.Sprite.DrawDepth;
            var drawDepthDifference = Shared.DrawDepth.DrawDepth.ThickPipe - (Shared.DrawDepth.DrawDepth.Puddles + 1);
            _sprite.SetDrawDepth((uid, args.Sprite), args.Sprite.DrawDepth - drawDepthDifference);
        }
        else if (component.OriginalDrawDepth.HasValue)
        {
            _sprite.SetDrawDepth((uid, args.Sprite), component.OriginalDrawDepth.Value);
            component.OriginalDrawDepth = null;
        }
    }

    private void UpdateAll()
    {
        var query = AllEntityQuery<SubFloorHideComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out _, out var appearance))
        {
            _appearance.QueueUpdate(uid, appearance);
        }
    }
}
