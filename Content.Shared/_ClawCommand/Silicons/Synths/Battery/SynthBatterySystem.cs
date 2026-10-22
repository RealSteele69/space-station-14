using System.Diagnostics.CodeAnalysis;
using Content.Shared.Body;
using Content.Shared.Power.Components;

namespace Content.Shared._ClawCommand.Silicons.Synths.Battery;

/// <summary>
/// Finds the power cell organ of a synth, and the synth a power cell organ belongs to.
/// </summary>
public sealed partial class SynthBatterySystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;

    public bool TryGetBattery(Entity<BodyComponent?> ent, [NotNullWhen(true)] out Entity<BatteryComponent>? battery)
    {
        battery = null;

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        foreach (var cell in _body.EnumerateOrgans<SynthPowerCellComponent>(ent))
        {
            if (!TryComp(cell.Owner, out BatteryComponent? batteryComp))
                continue;

            battery = (cell.Owner, batteryComp);
            return true;
        }

        return false;
    }

    public bool TryGetSynth(EntityUid cell, out Entity<SynthBatteryComponent> synth)
    {
        synth = default;

        if (!TryComp(cell, out OrganComponent? organ)
            || organ.Body is not { } body
            || !TryComp(body, out SynthBatteryComponent? synthBattery))
            return false;

        synth = (body, synthBattery);
        return true;
    }
}
