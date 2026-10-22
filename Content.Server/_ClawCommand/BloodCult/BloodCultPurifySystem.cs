using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared._ClawCommand.BloodCult.EntityEffects;
using Content.Shared.EntityEffects;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.BloodCult;

/// <summary>
/// A cultist being purged of the cult by holy water. Deconverted once <see cref="EndTime"/> passes.
/// </summary>
[RegisterComponent]
public sealed partial class BloodCultPurifyingComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;
}

public sealed partial class PurifyBloodCultEntityEffectSystem : EntityEffectSystem<BloodCultistComponent, PurifyBloodCult>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    protected override void Effect(Entity<BloodCultistComponent> entity, ref EntityEffectEvent<PurifyBloodCult> args)
    {
        if (HasComp<BloodCultPurifyingComponent>(entity))
            return;

        var purifying = AddComp<BloodCultPurifyingComponent>(entity);
        purifying.EndTime = _timing.CurTime + entity.Comp.PurifyTime;

        _jitter.DoJitter(entity, entity.Comp.PurifyTime, true, 10f, 4f);
        _popup.PopupEntity(Loc.GetString("blood-cult-purifying"), entity, entity, PopupType.MediumCaution);
    }
}

public sealed partial class BloodCultPurifySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloodCultPurifyingComponent>();
        while (query.MoveNext(out var uid, out var purifying))
        {
            if (purifying.EndTime > _timing.CurTime)
                continue;

            RemCompDeferred(uid, purifying);
            RemCompDeferred<BloodCultistComponent>(uid);
        }
    }
}
