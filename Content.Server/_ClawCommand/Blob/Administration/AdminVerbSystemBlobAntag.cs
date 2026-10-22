using Content.Server.Administration.Managers;
using Content.Shared._ClawCommand.Blob.Components;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._ClawCommand.Blob.Administration;

/// <summary>
/// Adds a "Make Blob Carrier" admin verb next to the other antag verbs.
/// The target gets the usual ten minutes (or the transform action) before it bursts into a core.
/// A target nobody is playing becomes a ghost role instead.
/// </summary>
public sealed partial class AdminVerbSystemBlobAntag : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddBlobVerb);
    }

    private void AddBlobVerb(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (!_adminManager.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
            return;

        if (!HasComp<MobStateComponent>(args.Target) || HasComp<BlobCarrierComponent>(args.Target))
            return;

        var target = args.Target;
        var name = Loc.GetString("admin-verb-text-make-blob");
        args.Verbs.Add(new Verb
        {
            Text = name,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_ClawCommand/Blob/Interface/Misc/antag_icon.rsi"), "blob"),
            Act = () =>
            {
                EnsureComp<BlobCarrierComponent>(target).HasMind = HasComp<ActorComponent>(target);
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", name, Loc.GetString("admin-verb-make-blob")),
        });
    }
}
