// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Religion.Nullrod;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Revenant.Components;
using Content.Shared.Silicon.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Religion.OnPray;

public sealed partial class HealNearOnPraySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ExamineSystemShared _examine = default!;

    private static readonly ProtoId<DamageTypePrototype> Holy = "Holy";

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealNearOnPrayComponent, AlternatePrayEvent>(OnPray);
    }

    private void OnPray(Entity<HealNearOnPrayComponent> ent, ref AlternatePrayEvent args)
    {
        var quiet = new AudioParams(-2f, 1f, SharedAudioSystem.DefaultSoundRange, 1f, false, 0f);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(Transform(args.User).Coordinates, ent.Comp.Range, _mobs);

        foreach (var mob in _mobs)
        {
            if (_mobState.IsDead(mob)
                || HasComp<SiliconComponent>(mob)
                || HasComp<BorgChassisComponent>(mob)
                || !_examine.InRangeUnOccluded(args.User, mob, ent.Comp.Range))
                continue;

            // Incorporeal spirits are out of reach until revealed.
            if (HasComp<SpectralComponent>(mob) && !HasComp<CorporealComponent>(mob))
                continue;

            // Holy damage only lands on the unholy (devils, cultists, ghouls, spirits...), so it doubles as the test.
            if (_damageable.CanBeDamagedBy(mob.Owner, Holy))
            {
                _damageable.TryChangeDamage(mob.Owner, ent.Comp.Damage, origin: args.User);
                Spawn(ent.Comp.DamageEffect, Transform(mob).Coordinates);
                _audio.PlayPvs(ent.Comp.SizzleSoundPath, mob, quiet);
            }
            else
            {
                _damageable.TryChangeDamage(mob.Owner, ent.Comp.Healing, true, origin: args.User);
                Spawn(ent.Comp.HealEffect, Transform(mob).Coordinates);
            }
        }

        _audio.PlayPvs(ent.Comp.HealSoundPath, ent, quiet);
    }
}
