using Content.Shared.Interaction;
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
}
