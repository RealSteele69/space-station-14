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
using Robust.Shared.Timing;

namespace Content.Client._GoobStation.Slasher.Systems;

/// <summary>
/// Registers the SlasherStaggerOverlay while any stagger shockwave is active.
/// </summary>
public sealed partial class SlasherStaggerOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SlasherStaggerOverlay _overlay = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlasherStaggerOverlayComponent, ComponentInit>(OnStaggerInit);
        SubscribeLocalEvent<SlasherStaggerOverlayComponent, ComponentShutdown>(OnStaggerShutdown);

        Subs.CVar(_cfg, CCVars.NoVisionFilters, OnNoVisionFiltersChanged);
    }

    private void OnStaggerInit(Entity<SlasherStaggerOverlayComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity != ent.Owner)
            ent.Comp.LocalStartTime = _timing.RealTime;

        if (!_cfg.GetCVar(CCVars.NoVisionFilters))
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnStaggerShutdown(Entity<SlasherStaggerOverlayComponent> ent, ref ComponentShutdown args)
    {
        if (Count<SlasherStaggerOverlayComponent>() <= 1)
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnNoVisionFiltersChanged(bool enabled)
    {
        if (enabled)
            _overlayMan.RemoveOverlay(_overlay);
        else if (Count<SlasherStaggerOverlayComponent>() > 0)
            _overlayMan.AddOverlay(_overlay);
    }
}
