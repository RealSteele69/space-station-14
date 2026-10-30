using Content.Shared.Eye;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Throwing;

namespace Content.Shared._ClawCommand.VentCrawl;

/// <summary>
/// Claw Command - while you are wedged in a pipe you can talk and emote, but you cannot reach out and touch anything.
/// You also see everything under the floor, like the sandbox "show subfloor" toggle.
/// </summary>
public sealed partial class SharedVentCrawlSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawlingComponent, InteractionAttemptEvent>(OnInteractAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, DropAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<VentCrawlingComponent, GetVisMaskEvent>(OnGetVisMask);
    }

    /// <summary>
    /// Pulls subfloor entities (pipes, cables, disposal tubes) over PVS while crawling. Set through
    /// <see cref="SharedEyeSystem.RefreshVisibilityMask"/> rather than a one-off mask write, so other systems
    /// refreshing the mask (t-rays, disposals, ghosting) can't strip it mid-crawl.
    /// </summary>
    private void OnGetVisMask(Entity<VentCrawlingComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Subfloor;
    }

    private void OnInteractAttempt(Entity<VentCrawlingComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnAttempt(EntityUid uid, VentCrawlingComponent comp, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }
}
