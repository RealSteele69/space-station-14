using System.Linq;
using Content.Shared.EntityTable;
using Content.Shared.Storage.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Handles the spooky-season "booby trapped closet" prank: the first time a
/// <see cref="SpookyClosetTrapComponent"/> closet is opened, it jump-scares whoever opened it and
/// then disarms itself. Closets are armed at round start by <see cref="TrickOrTreatCelebrate"/>.
/// </summary>
public sealed partial class SpookyClosetTrapSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpookyClosetTrapComponent, StorageBeforeOpenEvent>(OnBeforeOpen);
    }

    private void OnBeforeOpen(Entity<SpookyClosetTrapComponent> ent, ref StorageBeforeOpenEvent args)
    {
        var (uid, comp) = ent;

        // Disarm immediately so a second opener (or the same one re-opening) can't trigger it twice.
        RemComp<SpookyClosetTrapComponent>(uid);

        _audio.PlayPvs(comp.JumpscareSound, uid);

        if (comp.Effects is null)
            return;

        var effect = _entityTable.GetSpawns(comp.Effects).FirstOrDefault();
        if (effect != default)
            Spawn(effect, Transform(uid).Coordinates);
    }
}
