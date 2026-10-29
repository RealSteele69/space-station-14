// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Client._GoobStation.Slasher.Overlays;
using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._GoobStation.Slasher.Systems;

/// <summary>
/// Plays the SlasherRegenerateOverlay.
/// </summary>
public sealed partial class SlasherRegenerateOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly SlasherRegenerateOverlay _overlay = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherRegenerateOverlayComponent, ComponentInit>(OnRegenerateOverlayInit);
        SubscribeLocalEvent<SlasherRegenerateOverlayComponent, ComponentShutdown>(OnRegenerateOverlayShutdown);
        SubscribeLocalEvent<SlasherRegenerateOverlayComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SlasherRegenerateOverlayComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_cfg, CCVars.NoVisionFilters, OnNoVisionFiltersChanged);
    }

    private void OnRegenerateOverlayInit(Entity<SlasherRegenerateOverlayComponent> ent, ref ComponentInit args)
    {
        if (ent.Owner == _player.LocalEntity && !_cfg.GetCVar(CCVars.NoVisionFilters))
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnRegenerateOverlayShutdown(Entity<SlasherRegenerateOverlayComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _player.LocalEntity)
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<SlasherRegenerateOverlayComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        if (!_cfg.GetCVar(CCVars.NoVisionFilters))
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<SlasherRegenerateOverlayComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnNoVisionFiltersChanged(bool enabled)
    {
        if (enabled)
            _overlayMan.RemoveOverlay(_overlay);
        else if (HasComp<SlasherRegenerateOverlayComponent>(_player.LocalEntity))
            _overlayMan.AddOverlay(_overlay);
    }
}
