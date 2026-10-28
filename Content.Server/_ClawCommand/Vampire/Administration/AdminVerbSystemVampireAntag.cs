// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._ClawCommand.Vampire.GameTicking.Rules;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.Vampire.Administration;

/// <summary>
/// Adds a "Make Vampire" admin verb alongside the upstream Make-Traitor / Make-Zombie etc. verbs.
/// Uses the same ForceMakeAntag pipeline as the others.
/// </summary>
public sealed partial class AdminVerbSystemVampireAntag : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;

    private static readonly EntProtoId DefaultVampireRule = "Vampire";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddVampireVerb);
    }

    private void AddVampireVerb(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (!_adminManager.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        var targetPlayer = targetActor.PlayerSession;

        var vampireName = Loc.GetString("admin-verb-text-make-vampire");
        Verb vampire = new()
        {
            Text = vampireName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Actions/changeling.rsi"), "devour"),
            Act = () =>
            {
                _antag.ForceMakeAntag<VampireRuleComponent>(targetPlayer, DefaultVampireRule);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", vampireName, Loc.GetString("admin-verb-make-vampire")),
        };
        args.Verbs.Add(vampire);
    }
}
