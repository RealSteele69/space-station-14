using Content.Server.Popups;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._ClawCommand.Seasonal;

public sealed partial class ChristmasCrackerSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChristmasCrackerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<ChristmasCrackerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        if (!HasComp<HandsComponent>(target))
            return;

        var user = args.User;
        args.Handled = true;

        // Needs a second puller who is awake and has a hand free for their end.
        if (!_mobState.IsAlive(target) || !_hands.TryGetEmptyHand(target, out _))
        {
            _popup.PopupEntity(Loc.GetString("christmas-cracker-need-hand", ("target", target)), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("christmas-cracker-pop-others", ("user", user), ("target", target)),
            user, Filter.PvsExcept(user).RemovePlayerByAttachedEntity(target), true);
        _popup.PopupEntity(Loc.GetString("christmas-cracker-pop-self", ("target", target)), user, user);
        _popup.PopupEntity(Loc.GetString("christmas-cracker-pop-self", ("target", user)), target, target);
        _audio.PlayPvs(ent.Comp.Sound, user);

        var userCoords = _transform.GetMapCoordinates(user);
        var targetCoords = _transform.GetMapCoordinates(target);
        Spawn(ent.Comp.Joke, userCoords);
        Spawn(ent.Comp.Hat, targetCoords);

        var targetHalf = Spawn(ent.Comp.TargetHalf, targetCoords);
        _hands.TryPickupAnyHand(target, targetHalf, checkActionBlocker: false);

        // Swap the whole cracker in the puller's hand for their half.
        _hands.IsHolding(user, ent.Owner, out var hand);
        _hands.TryDrop(user, ent.Owner, checkActionBlocker: false, doDropInteraction: false);
        QueueDel(ent);

        var userHalf = Spawn(ent.Comp.UserHalf, userCoords);
        if (hand == null || !_hands.TryPickup(user, userHalf, hand, checkActionBlocker: false))
            _hands.TryPickupAnyHand(user, userHalf, checkActionBlocker: false);
    }
}
