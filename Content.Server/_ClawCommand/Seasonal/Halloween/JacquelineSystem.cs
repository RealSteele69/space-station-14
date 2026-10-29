using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Dataset;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Drives Jacqueline the Pumpqueen's trick-or-treat trading, ambient barks, and her habit of
/// turning up wherever the crew happens to be.
/// </summary>
public sealed partial class JacquelineSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private Content.Server.Chat.Systems.ChatSystem _chat = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JacquelineComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<JacquelineComponent, InteractUsingEvent>(OnInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<JacquelineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime >= comp.NextBarkTime)
            {
                comp.NextBarkTime = curTime + comp.BarkCooldown;
                Bark(uid, comp, comp.GreetLines, chance: 0.3f);
            }

            if (curTime >= comp.NextTeleportTime)
            {
                comp.NextTeleportTime = curTime + comp.TeleportCooldown;
                TeleportToCrowd(uid, comp);
            }
        }
    }

    /// <summary>
    /// Picks a random living crew member and moves Jacqueline to just outside their immediate
    /// vicinity, instead of leaving her to wander the whole station via the idle compound alone.
    /// </summary>
    private void TeleportToCrowd(EntityUid uid, JacquelineComponent comp)
    {
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var playerUid, out _, out var state, out _))
        {
            if (_mobState.IsAlive(playerUid, state))
                candidates.Add(playerUid);
        }

        if (candidates.Count == 0)
            return;

        // Don't let anyone holding onto her hitch a ride to wherever she's teleporting.
        if (TryComp<PullableComponent>(uid, out var pullable))
            _pulling.TryStopPull(uid, pullable, ignoreGrab: true);

        var target = _random.Pick(candidates);
        var targetCoords = Transform(target).Coordinates;
        var offset = _random.NextVector2(comp.TeleportMinRange, comp.TeleportMaxRange);
        _transform.SetCoordinates(uid, targetCoords.Offset(offset));
    }

    private void OnInteractHand(Entity<JacquelineComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Bark(ent, ent.Comp, ent.Comp.GreetLines, chance: 1f);
    }

    private void OnInteractUsing(Entity<JacquelineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var (uid, comp) = ent;

        if (!TryPrototype(args.Used, comp.Payment))
        {
            Bark(uid, comp, comp.NoCandyLines, chance: 1f);
            return;
        }

        args.Handled = true;

        var curTime = _timing.CurTime;
        if (curTime < comp.NextTradeTime)
            return;

        comp.NextTradeTime = curTime + comp.TradeCooldown;

        QueueDel(args.Used);

        if (_random.Prob(comp.TrickChance))
        {
            Trick(args.User, comp);
            Bark(uid, comp, comp.TrickLines, chance: 1f);
            return;
        }

        if (comp.Treats.Count > 0)
        {
            var reward = _random.Pick(comp.Treats);
            Spawn(reward, Transform(args.User).Coordinates);
        }

        Bark(uid, comp, comp.TreatLines, chance: 1f);
    }

    private bool TryPrototype(EntityUid uid, EntProtoId expected)
    {
        return MetaData(uid).EntityPrototype?.ID == expected.Id;
    }

    private void Trick(EntityUid target, JacquelineComponent comp)
    {
        var slot = _random.Prob(0.5f) ? "outerClothing" : "head";
        var item = slot == "outerClothing" ? comp.TrickCostume : comp.TrickHat;

        var spawned = Spawn(item, Transform(target).Coordinates);
        if (!_inventory.TryEquip(target, spawned, slot, silent: true, force: true))
        {
            Del(spawned);
            return;
        }

        EnsureComp<SelfUnremovableClothingComponent>(spawned);
    }

    private void Bark(EntityUid uid, JacquelineComponent comp, ProtoId<LocalizedDatasetPrototype> set, float chance)
    {
        if (chance < 1f && !_random.Prob(chance))
            return;

        if (!_proto.Resolve(set, out var dataset))
            return;

        var message = '>' + _random.Pick(dataset);
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, hideChat: false);
    }
}
