// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.GameTicking.Rules;
using Content.Shared._ClawCommand.Vampire;
using Content.Shared._ClawCommand.Vampire.Roles;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Antag;
using Content.Shared.GameTicking.Rules;

namespace Content.Server._ClawCommand.Vampire.GameTicking.Rules;

[RegisterComponent, Access(typeof(VampireRuleSystem))]
public sealed partial class VampireRuleComponent : Component
{
    [DataField]
    public SoundSpecifier? BriefingSound;

    [DataField]
    public ProtoId<NpcFactionPrototype> VampireFaction = "VampireFaction";

    [DataField]
    public ProtoId<NpcFactionPrototype> NanotrasenFaction = "NanoTrasen";
}

public sealed partial class VampireRuleSystem : GameRuleSystem<VampireRuleComponent>
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
    }

    private void OnSelectAntag(Entity<VampireRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeVampire(args.EntityUid, ent.Comp);
    }

    private void MakeVampire(EntityUid target, VampireRuleComponent rule)
    {
        var vampire = EnsureComp<VampireComponent>(target);

        GenerateFledglingFlavor(vampire);

        var fullName = Loc.GetString("vampire-fullname", ("title", vampire.VampireTitle ?? ""), ("name", vampire.VampireName ?? ""));
        var briefing = Loc.GetString("vampire-role-greeting", ("fullName", fullName.Trim()));
        _antag.SendBriefing(target, briefing, Color.Crimson, rule.BriefingSound);

        if (_mind.TryGetMind(target, out var mindId, out _)
            && _role.MindHasRole<VampireRoleComponent>(mindId, out var role))
            AddComp(role.Value, new RoleBriefingComponent { Briefing = briefing }, overwrite: true);

        _npcFaction.RemoveFaction(target, rule.NanotrasenFaction);
        _npcFaction.AddFaction(target, rule.VampireFaction);
    }

    private void GenerateFledglingFlavor(VampireComponent vampire)
    {
        vampire.IsMale = _random.Prob(0.5f);
        var firstNames = _prototype.Index(vampire.IsMale ? vampire.FirstNameMale : vampire.FirstNameFemale);
        vampire.VampireName = _random.Pick(firstNames.Values);

        var reputations = _prototype.Index(vampire.ReputationFledgling);
        vampire.VampireReputation = _random.Pick(reputations.Values);

        // Fledglings have earned no title yet - that comes the first time they rank up.
        vampire.VampireTitle = null;
    }
}
