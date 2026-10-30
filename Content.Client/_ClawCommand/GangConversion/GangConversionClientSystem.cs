using Content.Shared._ClawCommand.GangConversion.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._ClawCommand.GangConversion;

/// <summary>
/// Gang status icons, so fellow gangers (and ghosts/admins) can see who's who at a glance.
/// Mirrors <c>RevolutionarySystem</c>/<c>BloodCultClientSystem</c>'s icon pattern.
/// </summary>
public sealed partial class GangConversionClientSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GangMemberComponent, GetStatusIconsEvent>(OnMemberIcons);
        SubscribeLocalEvent<GangBossComponent, GetStatusIconsEvent>(OnBossIcons);
    }

    private void OnMemberIcons(Entity<GangMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        // The boss shows their own icon instead.
        if (HasComp<GangBossComponent>(ent))
            return;

        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnBossIcons(Entity<GangBossComponent> ent, ref GetStatusIconsEvent args)
    {
        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var icon))
            args.StatusIcons.Add(icon);
    }
}
