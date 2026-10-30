using Content.Server._ClawCommand.BloodCult.Rule;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Antag;

namespace Content.Server._ClawCommand.BloodCult.Administration;

/// <summary>
/// Adds "Make Blood Cultist" to the admin antag verbs, next to the upstream Make-Traitor etc.
/// </summary>
public sealed partial class AdminVerbSystemBloodCultAntag : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;

    private static readonly EntProtoId DefaultBloodCultRule = "BloodCult";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddBloodCultVerb);
    }

    private void AddBloodCultVerb(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !_adminManager.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        var targetPlayer = targetActor.PlayerSession;
        var name = Loc.GetString("admin-verb-text-make-blood-cultist");
        args.Verbs.Add(new Verb
        {
            Text = name,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_ClawCommand/BloodCult/cult_hud.rsi"), "cult_member"),
            Act = () => _antag.ForceMakeAntag<BloodCultRuleComponent>(targetPlayer, DefaultBloodCultRule),
            Impact = LogImpact.High,
            Message = string.Join(": ", name, Loc.GetString("admin-verb-make-blood-cultist")),
        });
    }
}
