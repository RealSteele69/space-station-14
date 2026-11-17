// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Timing.Components;
using Content.Shared.Timing.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Server._ClawCommand.Weapons.KnockdownOnHit;

public sealed partial class KnockdownOnHitSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private UseDelaySystem _delay = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KnockdownOnHitComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<KnockdownOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!ent.Comp.KnockdownOnHeavyAttack && args.Direction != null)
            return;

        if (ent.Comp.UseDelayId is { } delayId
            && TryComp<UseDelayComponent>(ent, out var delay)
            && _delay.IsDelayed((ent, delay), delayId))
            return;

        var knockedDown = false;
        foreach (var target in args.HitEntities)
        {
            if (HasComp<BorgChassisComponent>(target) || !_mobState.IsAlive(target))
                continue;

            if (!_stun.TryKnockdown(target, ent.Comp.Duration, ent.Comp.RefreshDuration, ent.Comp.AutoStand, ent.Comp.DropItems))
                continue;

            knockedDown = true;
            _adminLog.Add(LogType.DisarmedKnockdown, LogImpact.Medium,
                $"{ToPrettyString(args.User):player} knocked down {ToPrettyString(target):target} with {ToPrettyString(ent):weapon}");

            if (ent.Comp.KnockdownEffect is { } effect)
                Spawn(effect, Transform(target).Coordinates);
        }

        if (!knockedDown)
            return;

        if (ent.Comp.UseDelayId is { } id)
            _delay.TryResetDelay(ent.Owner, id: id);

        _audio.PlayPvs(ent.Comp.KnockdownSound, ent);
    }
}
