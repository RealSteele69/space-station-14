using Content.Server._ClawCommand.CosmicCult.Components;
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

namespace Content.Server._ClawCommand.CosmicCult.Administration;

/// <summary>
/// Adds a "Make Cosmic Cultist" admin verb alongside the upstream Make-Traitor / Make-Zombie etc. verbs.
/// Uses the same ForceMakeAntag pipeline; the target becomes the cult's first member (and gets the monument).
/// </summary>
public sealed partial class AdminVerbSystemCosmicCultAntag : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;

    private static readonly EntProtoId DefaultCosmicCultRule = "CosmicCult";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddCosmicCultVerb);
    }

    private void AddCosmicCultVerb(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (!_adminManager.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        var targetPlayer = targetActor.PlayerSession;

        var cosmicCultName = Loc.GetString("admin-verb-text-make-cosmiccultist");
        Verb cosmicCult = new()
        {
            Text = cosmicCultName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_DV/CosmicCult/Icons/antag_icons.rsi"), "CosmicCult"),
            Act = () =>
            {
                _antag.ForceMakeAntag<CosmicCultRuleComponent>(targetPlayer, DefaultCosmicCultRule);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", cosmicCultName, Loc.GetString("admin-verb-make-cosmiccultist")),
        };
        args.Verbs.Add(cosmicCult);
    }
}
