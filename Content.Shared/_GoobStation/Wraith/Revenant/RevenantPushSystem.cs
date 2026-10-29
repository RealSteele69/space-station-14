// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source used Content.Shared._White.Grab.GrabThrownSystem.Throw (another fork's throw+damage
// helper, doesn't exist here). Substituted with the vanilla ThrowingSystem.TryThrow for the shove, plus
// a direct DamageableSystem.TryChangeDamage applied at throw-time for DamageWhenThrown (source applied it
// on landing-impact; this fires immediately, which is simpler and still means "pushing someone with this
// hurts them").

using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._GoobStation.Wraith.Revenant;

/// <summary>
/// Shoves the targeted victim or object away from you, dealing damage.
/// </summary>
public sealed partial class RevenantPushSystem : EntitySystem
{
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantPushComponent, RevenantPushEvent>(OnRevPush);
    }

    private void OnRevPush(Entity<RevenantPushComponent> ent, ref RevenantPushEvent args)
    {
        var entPos = _transform.GetMapCoordinates(ent).Position;
        var targetPos = _transform.GetMapCoordinates(args.Target).Position;
        var direction = targetPos - entPos;

        _throwing.TryThrow(args.Target, direction, ent.Comp.ThrowSpeed, ent.Owner);

        if (ent.Comp.DamageWhenThrown != null)
            _damageable.TryChangeDamage(args.Target, ent.Comp.DamageWhenThrown);

        _audio.PlayPredicted(ent.Comp.RevPushSound, ent.Owner, ent.Owner);

        args.Handled = true;
    }
}
