using Content.Shared._ClawCommand.Silicons.Synths;
using Content.Shared._ClawCommand.Silicons.Synths.Battery;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Power.EntitySystems;

namespace Content.Server._ClawCommand.Silicons.Synths;

/// <summary>
/// Shock damage charges a synth's power cell and deals a bit of cellular damage on top.
/// </summary>
public sealed partial class SynthShockSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SynthBatterySystem _synthBattery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SynthShockComponent, DamageDealtEvent>(OnDamageDealt);
    }

    private void OnDamageDealt(Entity<SynthShockComponent> ent, ref DamageDealtEvent args)
    {
        if (!args.Damage.DamageDict.TryGetValue(ent.Comp.ShockDamageType, out var shock) || shock <= 0)
            return;

        if (ent.Comp.BatteryChargeMultiplier > 0f && _synthBattery.TryGetBattery(ent.Owner, out var battery))
            _battery.ChangeCharge(battery.Value.AsNullable(), shock.Float() * ent.Comp.BatteryChargeMultiplier);

        if (ent.Comp.CellularDamageMultiplier <= 0f)
            return;

        var cellular = FixedPoint2.New(shock.Float() * ent.Comp.CellularDamageMultiplier);
        if (cellular <= 0)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict[ent.Comp.CellularDamageType] = cellular;
        _damageable.TryChangeDamage(ent.Owner, damage, origin: args.Origin);
    }
}
