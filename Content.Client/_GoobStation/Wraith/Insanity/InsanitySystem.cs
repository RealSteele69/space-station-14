// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Other;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._GoobStation.Wraith.Insanity;

public sealed partial class InsanitySystem : EntitySystem
{
    private InsanityOverlay _overlay = default!;

    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WraithInsanityComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<WraithInsanityComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<WraithInsanityComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<WraithInsanityComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnPlayerAttached(Entity<WraithInsanityComponent> ent, ref LocalPlayerAttachedEvent args) =>
        _overlayManager.AddOverlay(_overlay);

    private void OnPlayerDetached(Entity<WraithInsanityComponent> ent, ref LocalPlayerDetachedEvent args) =>
        _overlayManager.RemoveOverlay(_overlay);

    private void OnInit(Entity<WraithInsanityComponent> ent, ref ComponentInit args)
    {
        if (_playerManager.LocalEntity != ent.Owner)
            return;

        _overlay.SetValues(ent.Comp.Speed, ent.Comp.Radius, ent.Comp.Color);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnShutdown(Entity<WraithInsanityComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalEntity != ent.Owner)
            return;

        _overlayManager.RemoveOverlay(_overlay);
    }
}
