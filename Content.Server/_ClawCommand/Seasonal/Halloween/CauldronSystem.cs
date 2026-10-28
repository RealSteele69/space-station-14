using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Nuke;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Feeds junk items into a <see cref="CauldronComponent"/> cauldron, crystallizing them into
/// witches' candy every few feedings.
/// </summary>
public sealed partial class CauldronSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CauldronComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<CauldronComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Don't let people absent-mindedly feed their ID or PDA to the cauldron.
        if (HasComp<Content.Shared.PDA.PdaComponent>(args.Used) || HasComp<Content.Shared.Access.Components.IdCardComponent>(args.Used))
            return;

        // Nor anything the round depends on, anything with someone inside, or a bag full of other things.
        if (IsProtected(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("halloween-cauldron-refuse", ("item", args.Used)), ent, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var (uid, comp) = ent;

        QueueDel(args.Used);
        _audio.PlayPvs(comp.FeedSound, uid);
        comp.FeedProgress++;

        if (comp.FeedProgress < comp.FeedRequired)
        {
            _popup.PopupEntity(Loc.GetString("halloween-cauldron-feed"), uid, args.User);
            return;
        }

        comp.FeedProgress = 0;
        var amount = _random.Next(comp.RewardMin, comp.RewardMax + 1);
        for (var i = 0; i < amount; i++)
        {
            Spawn(comp.Reward, Transform(uid).Coordinates);
        }

        _audio.PlayPvs(comp.RewardSound, uid);
        _popup.PopupEntity(Loc.GetString("halloween-cauldron-reward"), uid, args.User);
    }

    private bool IsProtected(EntityUid used)
    {
        if (HasComp<UnremoveableComponent>(used)          // borg modules, implants, cursed gear
            || HasComp<StealTargetComponent>(used)        // objective items
            || HasComp<NukeDiskComponent>(used)
            || HasComp<BorgModuleComponent>(used)
            || HasComp<MobStateComponent>(used))          // held mice and other critters
            return true;

        // Brains, positronics, pAIs.
        if (TryComp<MindContainerComponent>(used, out var mind) && mind.HasMind)
            return true;

        return TryComp<StorageComponent>(used, out var storage) && storage.Container.ContainedEntities.Count > 0;
    }
}
