// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Hands;
using Content.Shared.Movement.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.Weapons.RandomizeMovementSpeed;

public sealed partial class ItemRandomizeMovementSpeedSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemRandomizeMovementspeedComponent, GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<ItemRandomizeMovementspeedComponent, GotUnequippedHandEvent>(OnGotUnequippedHand);
        SubscribeLocalEvent<ItemRandomizeMovementspeedComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMovementSpeedModifiers);
    }

    private void OnGotEquippedHand(Entity<ItemRandomizeMovementspeedComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.User = _whitelist.IsWhitelistPassOrNull(ent.Comp.Whitelist, args.User) ? args.User : null;
        Dirty(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnGotUnequippedHand(Entity<ItemRandomizeMovementspeedComponent> ent, ref GotUnequippedHandEvent args)
    {
        ent.Comp.User = null;
        ent.Comp.CurrentModifier = 1f;
        ent.Comp.TargetModifier = 1f;
        Dirty(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(args.User);
    }

    private void OnRefreshMovementSpeedModifiers(EntityUid uid, ItemRandomizeMovementspeedComponent comp, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (comp.User != null)
            args.Args.ModifySpeed(comp.CurrentModifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ItemRandomizeMovementspeedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.User is not { } user)
                continue;

            if (_net.IsServer && _timing.CurTime >= comp.NextExecutionTime)
            {
                comp.TargetModifier = _random.NextFloat(comp.Min, comp.Max);
                comp.NextExecutionTime = _timing.CurTime + comp.ExecutionInterval;
                Dirty(uid, comp);
            }

            if (MathHelper.CloseTo(comp.CurrentModifier, comp.TargetModifier, 0.01f))
                continue;

            comp.CurrentModifier = MathHelper.Lerp(comp.CurrentModifier, comp.TargetModifier, Math.Min(1f, frameTime / comp.SmoothingTime));
            _movementSpeed.RefreshMovementSpeedModifiers(user);
        }
    }
}
