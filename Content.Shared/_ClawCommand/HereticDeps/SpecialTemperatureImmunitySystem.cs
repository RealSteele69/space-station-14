// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Temperature.Components;
using Content.Shared.Temperature;

namespace Content.Shared._ClawCommand.HereticDeps;

/// <summary>
/// Makes <see cref="SpecialHighTempImmunityComponent"/> stop the entity heating up,
/// and <see cref="SpecialLowTempImmunityComponent"/> stop it cooling down.
/// </summary>
public sealed partial class SpecialTemperatureImmunitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpecialHighTempImmunityComponent, BeforeHeatExchangeEvent>(OnHighTempExchange);
        SubscribeLocalEvent<SpecialLowTempImmunityComponent, BeforeHeatExchangeEvent>(OnLowTempExchange);
    }

    private void OnHighTempExchange(Entity<SpecialHighTempImmunityComponent> ent, ref BeforeHeatExchangeEvent args)
    {
        if (!args.Cooling)
            args.HeatTransferModifier = 0f;
    }

    private void OnLowTempExchange(Entity<SpecialLowTempImmunityComponent> ent, ref BeforeHeatExchangeEvent args)
    {
        if (args.Cooling)
            args.HeatTransferModifier = 0f;
    }
}
