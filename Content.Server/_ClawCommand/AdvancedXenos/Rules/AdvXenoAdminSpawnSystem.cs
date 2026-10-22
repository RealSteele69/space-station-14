using Content.Server.Antag;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared._ClawCommand.AdvancedXenos;
using Content.Shared.GameTicking.Components;

namespace Content.Server._ClawCommand.AdvancedXenos.Rules;

public sealed partial class AdvXenoAdminSpawnSystem : EntitySystem
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private RoleSystem _role = default!;

    public override void Initialize()
    {
        base.Initialize();

        // After the ghost role system has made the mind and moved the player in.
        SubscribeLocalEvent<AdvXenoAdminSpawnComponent, TakeGhostRoleEvent>(OnTakeRole, after: [typeof(GhostRoleSystem)]);
    }

    private void OnTakeRole(Entity<AdvXenoAdminSpawnComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (!args.TookRole || args.Player.AttachedEntity != ent.Owner)
            return;

        // The ghost role adds the xeno mind role so antag bans apply; the antag system adds it again.
        if (_mind.TryGetMind(ent.Owner, out var mind, out _))
            _role.MindRemoveRole<AdvXenomorphRoleComponent>(mind);

        // The rule has no spawn locations, so the antag system keeps the player in this body.
        _antag.ForceMakeAntag<GameRuleComponent>(args.Player, ent.Comp.Rule, ent.Comp.Antag);
        RemCompDeferred<AdvXenoAdminSpawnComponent>(ent);
    }
}
