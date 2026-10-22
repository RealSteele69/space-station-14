// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;

namespace Content.Server._ClawCommand.Devil.DelayedDeath;

/// <summary>
/// Kills the entity once <see cref="DeathTime"/> seconds have passed. Used by the devil's "time" clause.
/// </summary>
[RegisterComponent]
public sealed partial class DelayedDeathComponent : Component
{
    /// <summary>
    /// How long it takes to kill the entity, in seconds.
    /// </summary>
    [DataField]
    public float DeathTime = 60;

    /// <summary>
    /// If true, the entity can't be revived afterwards, not even by cheating death.
    /// </summary>
    [DataField]
    public bool PreventAllRevives;

    /// <summary>
    /// How long it has been since the delayed death timer started.
    /// </summary>
    [ViewVariables]
    public float DeathTimer;

    /// <summary>
    /// What message is displayed when the time runs out.
    /// </summary>
    [DataField]
    public LocId? DeathMessageId;
}

/// <summary>
/// Raised on an entity when its delayed death kills it.
/// </summary>
[ByRefEvent]
public record struct DelayedDeathEvent(EntityUid User, bool Cancelled = false, bool PreventRevive = true);

public sealed partial class DelayedDeathSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DelayedDeathComponent, MobStateComponent>();
        while (query.MoveNext(out var ent, out var comp, out var mob))
        {
            comp.DeathTimer += frameTime;

            if (comp.DeathTimer < comp.DeathTime || _mobState.IsDead(ent, mob))
                continue;

            // go crit then dead so deathgasp can happen
            _mobState.ChangeMobState(ent, MobState.Critical, mob);
            _mobState.ChangeMobState(ent, MobState.Dead, mob);

            var ev = new DelayedDeathEvent(ent, PreventRevive: comp.PreventAllRevives);
            RaiseLocalEvent(ent, ref ev);

            if (ev.Cancelled)
            {
                RemCompDeferred<DelayedDeathComponent>(ent);
                continue;
            }

            if (comp.PreventAllRevives)
                EnsureComp<UnrevivableComponent>(ent);

            if (comp.DeathMessageId is { } message)
                _popup.PopupEntity(Loc.GetString(message), ent, ent, PopupType.LargeCaution);
        }
    }
}
