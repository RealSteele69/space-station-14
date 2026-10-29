// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: GhoulSystem still lives under namespace Content.Client._Shitcode.Heretic on this
// fork (only its folder moved to _ClawCommand). Also, GetStatusIconsEvent here has no "Uid" field
// - it's a record struct (List&lt;StatusIconData&gt; StatusIcons) raised as a per-entity directed
// event, so this subscribes on BoogeymanShadowComponent directly (like SoullessSystem does for
// GetStatusIconsEvent) instead of a broadcast subscription.

using Content.Client._Shitcode.Heretic;
using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._GoobStation.Slasher.Systems;

/// <summary>
/// Makes entities invisible in darkness and visible in light, and hides their status icons from everyone else.
/// </summary>
public sealed partial class BoogeymanShadowSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "SlasherBoogeyman";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoogeymanShadowComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BoogeymanShadowComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<BoogeymanShadowComponent, GetStatusIconsEvent>(OnGetStatusIcons, after: new[] { typeof(GhoulSystem) });
    }

    private void OnStartup(Entity<BoogeymanShadowComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.PostShader = _proto.Index(Shader).InstanceUnique();
    }

    private void OnShutdown(Entity<BoogeymanShadowComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.PostShader = null;
    }

    private void OnGetStatusIcons(Entity<BoogeymanShadowComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ent.Owner != _player.LocalEntity)
            args.StatusIcons.Clear();
    }
}
