// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._ClawCommand.Shadekin.Systems;
using Content.Shared._ClawCommand.Vampire;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Server.Containers;

namespace Content.Server._ClawCommand.Vampire;

public sealed partial class VampireSystem
{
    [Dependency] private ShadekinSystem _shadekin = default!;
    [Dependency] private ContainerSystem _container = default!;

    /// <summary>
    /// Vampires are photosensitive the same way Shadekin are - we just reuse their light-exposure scan
    /// instead of running a second one, and respond to it with real burn damage instead of an energy
    /// penalty. Anything enclosed (a coffin, any other container) blocks light entirely.
    /// </summary>
    private void ProcessSunlight(Entity<VampireComponent> vampire)
    {
        if (!_mobState.IsAlive(vampire) && !_mobState.IsCritical(vampire))
            return;

        if (_container.IsEntityInContainer(vampire))
        {
            vampire.Comp.WarnedSunlight = false;
            return;
        }

        var exposure = _shadekin.GetLightExposure(vampire);
        var tier = exposure switch
        {
            >= 20f => 4,
            >= 10f => 3,
            >= 5f => 2,
            >= 0.8f => 1,
            _ => 0,
        };

        if (tier < vampire.Comp.SunlightBurnTier)
        {
            vampire.Comp.WarnedSunlight = false;
            return;
        }

        if (!vampire.Comp.WarnedSunlight)
        {
            vampire.Comp.WarnedSunlight = true;
            _popup.PopupEntity(Loc.GetString("vampire-sunlight-warning"), vampire, vampire, PopupType.LargeCaution);
        }

        var burn = FixedPoint2.New(vampire.Comp.SunlightBurnPerTier * tier);
        var damage = new DamageSpecifier(_prototype.Index(vampire.Comp.BurnHealGroup), burn);
        _damageable.TryChangeDamage(vampire.Owner, damage, ignoreResistances: false, interruptsDoAfters: false);
    }
}
