using Content.Shared._ClawCommand.Silicons.Synths.Battery;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Body;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._ClawCommand.Silicons.Synths.Battery;

/// <summary>
/// Food a synth digests also tops up its power cell.
/// </summary>
public sealed partial class SynthBatteryMetabolismSystem : EntityEffectSystem<SynthBatteryComponent, Satiate>
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SynthBatterySystem _synthBattery = default!;

    protected override void Effect(Entity<SynthBatteryComponent> entity, ref EntityEffectEvent<Satiate> args)
    {
        if (args.Effect.SatiationType != SatiationSystem.Hunger)
            return;

        var charge = args.Effect.Factor * args.Scale * entity.Comp.NutrimentChargeMultiplier;
        if (charge <= 0f || !_synthBattery.TryGetBattery(entity.Owner, out var battery))
            return;

        _battery.ChangeCharge(battery.Value.AsNullable(), charge);
    }
}
