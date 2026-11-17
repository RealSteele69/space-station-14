// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Religion.Nullrod;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Server._ClawCommand.Religion.OnPray;

public sealed partial class ReloadOnPraySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReloadOnPrayComponent, AlternatePrayEvent>(OnPray);
    }

    private void OnPray(Entity<ReloadOnPrayComponent> ent, ref AlternatePrayEvent args)
    {
        if (!TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo) || ammo.Capacity is not { } capacity)
            return;

        if (_gun.UpdateBasicEntityAmmoCount((ent, ammo), capacity))
            _audio.PlayPvs(ent.Comp.ReloadSoundPath, ent);
    }
}
