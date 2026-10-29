// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._GoobStation.Wraith.Aura;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._GoobStation.Wraith.Aura;

/// <summary>
/// Handles the distortion aura shader around a possessing Wraith/Revenant.
/// </summary>
public sealed partial class AuraSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> Shader = "WraithAura";

    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string PostShaderId = "wraithaura";

    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _protoMan.Index(Shader).InstanceUnique();

        SubscribeLocalEvent<AuraComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AuraComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AuraComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    private void OnStartup(Entity<AuraComponent> ent, ref ComponentStartup args) =>
        SetShader((ent.Owner, ent.Comp), true);

    private void OnShutdown(Entity<AuraComponent> ent, ref ComponentShutdown args)
    {
        if (!Terminating(ent.Owner))
            SetShader((ent.Owner, ent.Comp), false);
    }

    private void SetShader(Entity<AuraComponent?> ent, bool enabled, SpriteComponent? sprite = null)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, ref sprite, false))
            return;

        if (!enabled)
        {
            _sprite.RemovePostShader((ent.Owner, sprite), PostShaderId);
            return;
        }

        _sprite.SetPostShader((ent.Owner, sprite), new SpriteComponent.PostShaderArgs(PostShaderId, _shader)
        {
            GetScreenTexture = true,
            RaiseShaderEvent = true,
            Before = ContentPostShaderIds.BeforeOutlines,
        });
    }

    private void OnShaderRender(Entity<AuraComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != PostShaderId)
            return;

        _shader.SetParameter("distortion", ent.Comp.Distortion);
        _shader.SetParameter("auraColor", new Vector3(ent.Comp.AuraColor.A, ent.Comp.AuraColor.R, ent.Comp.AuraColor.G));
        _shader.SetParameter("mango", ent.Comp.AuraFarm);
    }
}
