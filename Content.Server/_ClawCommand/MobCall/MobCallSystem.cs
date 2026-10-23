using Content.Server.Chat.Systems;
using Content.Server.NPC.Systems;
using Content.Shared._ClawCommand.MobCall;
using Content.Shared.Whitelist;

namespace Content.Server._ClawCommand.MobCall;

/// <summary>
///     Claw Command - Handles <see cref="MobCallActionEvent"/>: the caller emotes, and every whitelisted
///     callable mob in range gets the caller's position on its NPC blackboard to walk to.
/// </summary>
public sealed partial class MobCallSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobCallSourceComponent, MobCallActionEvent>(OnMobCall);
    }

    private void OnMobCall(Entity<MobCallSourceComponent> ent, ref MobCallActionEvent args)
    {
        if (args.Handled)
            return;

        _chat.TryEmoteWithChat(ent, ent.Comp.Emote, forceEmote: false);

        var mapCoords = _transform.GetMapCoordinates(ent);
        var entCoords = Transform(ent).Coordinates;
        foreach (var callable in _lookup.GetEntitiesInRange<MobCallableComponent>(mapCoords, ent.Comp.Range))
        {
            if (callable.Owner == ent.Owner)
                continue;

            if (_whitelist.IsWhitelistPass(ent.Comp.Whitelist, callable))
                _npc.SetBlackboard(callable, ent.Comp.Key, entCoords);
        }

        args.Handled = true;
    }
}
