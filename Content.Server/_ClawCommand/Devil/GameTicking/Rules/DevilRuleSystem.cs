// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared._ClawCommand.Devil;
using Content.Shared._ClawCommand.Devil.Roles;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Devil.GameTicking.Rules;

[RegisterComponent, Access(typeof(DevilRuleSystem))]
public sealed partial class DevilRuleComponent : Component
{
    [DataField]
    public SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/_ClawCommand/Devil/devil_start.ogg");

    [DataField]
    public ProtoId<NpcFactionPrototype> DevilFaction = "DevilFaction";

    [DataField]
    public ProtoId<NpcFactionPrototype> NanotrasenFaction = "NanoTrasen";
}

public sealed partial class DevilRuleSystem : GameRuleSystem<DevilRuleComponent>
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private ObjectivesSystem _objective = default!;
    [Dependency] private SharedRoleSystem _role = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
        SubscribeLocalEvent<DevilRuleComponent, ObjectivesTextPrependEvent>(OnTextPrepend);
    }

    private void OnSelectAntag(Entity<DevilRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeDevil(args.EntityUid, ent.Comp);
    }

    private void MakeDevil(EntityUid target, DevilRuleComponent rule)
    {
        var devilComp = EnsureComp<DevilComponent>(target);

        var briefing = Loc.GetString("devil-role-greeting", ("trueName", devilComp.TrueName), ("playerName", Name(target)));
        _antag.SendBriefing(target, briefing, Color.DarkRed, rule.BriefingSound);

        // Keep the briefing (and the true name) in the character menu.
        if (_mind.TryGetMind(target, out var mindId, out _)
            && _role.MindHasRole<DevilRoleComponent>(mindId, out var role))
            AddComp(role.Value, new RoleBriefingComponent { Briefing = briefing }, overwrite: true);

        _npcFaction.RemoveFaction(target, rule.NanotrasenFaction);
        _npcFaction.AddFaction(target, rule.DevilFaction);
    }

    private void OnTextPrepend(Entity<DevilRuleComponent> ent, ref ObjectivesTextPrependEvent args)
    {
        var mostContractsName = string.Empty;
        var mostContracts = 0;

        var query = EntityQueryEnumerator<DevilComponent>();
        while (query.MoveNext(out var devil, out var devilComp))
        {
            if (!_mind.TryGetMind(devil, out var mindId, out var mind) || devilComp.Souls < mostContracts)
                continue;

            mostContracts = devilComp.Souls;
            mostContractsName = _objective.GetTitle((mindId, mind), MetaData(devil).EntityName);
        }

        var locId = string.IsNullOrWhiteSpace(mostContractsName)
            ? "roundend-prepend-devil-contracts"
            : "roundend-prepend-devil-contracts-named";
        args.Text = Loc.GetString(locId, ("name", mostContractsName), ("number", mostContracts));
    }
}
