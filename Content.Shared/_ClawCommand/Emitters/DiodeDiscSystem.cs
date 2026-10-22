using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Singularity.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._ClawCommand.Emitters;

public sealed partial class DiodeDiscSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiodeDiscComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DiodeDiscComponent, DiodeDiscDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DiodeDiscInstalledComponent, ExaminedEvent>(OnInstalledExamined);
    }

    private void OnAfterInteract(Entity<DiodeDiscComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<EmitterComponent>(target) || HasComp<DiodeDiscInstalledComponent>(target))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DiodeDiscDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<DiodeDiscComponent> ent, ref DiodeDiscDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        if (!TryComp<EmitterComponent>(target, out var emitter) || HasComp<DiodeDiscInstalledComponent>(target))
            return;

        args.Handled = true;

        emitter.BoltType = ent.Comp.NewBolt;
        Dirty(target, emitter);

        var installed = EnsureComp<DiodeDiscInstalledComponent>(target);
        installed.Upgrade = ent.Comp.Upgrade;
        Dirty(target, installed);

        _audio.PlayPredicted(ent.Comp.UpgradeSound, target, args.User);
        PredictedQueueDel(ent.Owner);
    }

    private void OnInstalledExamined(Entity<DiodeDiscInstalledComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Upgrade));
    }
}
