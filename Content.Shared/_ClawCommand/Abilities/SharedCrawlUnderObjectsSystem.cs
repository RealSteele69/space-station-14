using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Popups;

namespace Content.Shared._ClawCommand.Abilities;

public abstract partial class SharedCrawlUnderObjectsSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrawlUnderObjectsComponent, CrawlingUpdatedEvent>(OnCrawlingUpdated);
        // The climb attempt is raised on the thing being climbed, not on the climber.
        SubscribeLocalEvent<ClimbableComponent, AttemptClimbEvent>(OnAttemptClimb);
    }

    private void OnCrawlingUpdated(Entity<CrawlUnderObjectsComponent> ent, ref CrawlingUpdatedEvent args)
    {
        // Only the sneaker is told; announcing it to the room would defeat the point.
        _popup.PopupEntity(Loc.GetString(args.Enabled
                ? "crawl-under-objects-toggle-on"
                : "crawl-under-objects-toggle-off"),
            ent,
            ent);
    }

    // No vaulting onto the table you are hiding under.
    private void OnAttemptClimb(Entity<ClimbableComponent> ent, ref AttemptClimbEvent args)
    {
        if (TryComp<CrawlUnderObjectsComponent>(args.Climber, out var crawl) && crawl.Enabled)
            args.Cancelled = true;
    }
}
