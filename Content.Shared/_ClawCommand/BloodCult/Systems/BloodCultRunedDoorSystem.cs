using System.Numerics;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Doors;
using Content.Shared.Interaction;
using Content.Shared.Prying.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.BloodCult.Systems;

/// <summary>
/// Runed doors only answer to the cult. Anyone else who lays a hand on one is thrown back.
/// </summary>
public sealed partial class BloodCultRunedDoorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BloodCultItemSystem _cultItem = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultRunedDoorComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpened);
        SubscribeLocalEvent<BloodCultRunedDoorComponent, BeforePryEvent>(OnBeforePry);
        SubscribeLocalEvent<BloodCultRunedDoorComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnBeforeDoorOpened(Entity<BloodCultRunedDoorComponent> door, ref BeforeDoorOpenedEvent args)
    {
        if (args.User is { } user && !_cultItem.IsCultMember(user))
            args.Cancel();
    }

    private void OnBeforePry(Entity<BloodCultRunedDoorComponent> door, ref BeforePryEvent args)
    {
        if (_cultItem.IsCultMember(args.User))
            return;

        args.Cancelled = true;
        args.Message = "cult-runed-door-pry-fail";
    }

    private void OnInteractHand(Entity<BloodCultRunedDoorComponent> door, ref InteractHandEvent args)
    {
        if (args.Handled || _cultItem.IsCultMember(args.User) || !_timing.IsFirstTimePredicted)
            return;

        args.Handled = true;

        var away = _transform.GetWorldPosition(args.User) - _transform.GetWorldPosition(door);
        if (away.LengthSquared() < 0.01f)
            away = Vector2.UnitY;

        _stun.TryKnockdown(args.User, door.Comp.RepulseKnockdown, true);
        _throwing.TryThrow(args.User, Vector2.Normalize(away) * door.Comp.RepulseDistance, door.Comp.RepulseStrength, door, recoil: false);
    }
}
