using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;

namespace Content.Server._ClawCommand.Undergarment;

/// <summary>
/// CLAW COMMAND - undergarment markings cover what's under them. The genital marking layers draw above
/// clothing, so underwear sitting on the lower <see cref="HumanoidVisualLayers.UndergarmentBottom"/> layer
/// can't cover them by draw order alone; instead, while a body has a bottom on, its genital and penis
/// layers are occluded the same way a uniform occludes them, and a top does the same for the breasts.
/// Taking undergarments off (see <see cref="UndergarmentRemovalSystem"/>) strips the markings, which
/// lifts the occlusion again.
/// </summary>
public sealed partial class UndergarmentOcclusionSystem : EntitySystem
{
    /// <summary>
    /// Not a real equipment slot - a bit of its own, so this never gets mixed up with (or cleared by)
    /// a uniform that hides the same layers. Taking the jumpsuit off leaves the underwear's occlusion on.
    /// </summary>
    private const SlotFlags UndergarmentSource = (SlotFlags) (1 << 20);

    [Dependency] private MarkingManager _marking = default!;
    [Dependency] private SharedHideableHumanoidLayersSystem _hideableLayers = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        // After the body has relayed the new markings onto its organs, so we read the updated set.
        // This also covers spawning, since the initial appearance is applied through the same event.
        SubscribeLocalEvent<VisualBodyComponent, ApplyOrganMarkingsEvent>(OnMarkingsApplied, after: [typeof(BodySystem)]);
        SubscribeLocalEvent<VisualBodyComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<VisualBodyComponent, OrganRemovedFromEvent>(OnOrganRemoved);
    }

    private void OnMarkingsApplied(Entity<VisualBodyComponent> ent, ref ApplyOrganMarkingsEvent args)
    {
        UpdateOcclusion(ent);
    }

    private void OnOrganInserted(Entity<VisualBodyComponent> ent, ref OrganInsertedIntoEvent args)
    {
        UpdateOcclusion(ent);
    }

    private void OnOrganRemoved(Entity<VisualBodyComponent> ent, ref OrganRemovedFromEvent args)
    {
        UpdateOcclusion(ent);
    }

    private void UpdateOcclusion(Entity<VisualBodyComponent> ent)
    {
        if (!TryComp<HideableHumanoidLayersComponent>(ent, out var hideable))
            return;

        if (!_visualBody.TryGatherMarkingsData(ent.AsNullable(), null, out _, out _, out var applied))
            return;

        var hasBottom = false;
        var hasTop = false;

        foreach (var layers in applied.Values)
        {
            foreach (var markings in layers.Values)
            {
                foreach (var marking in markings)
                {
                    if (!_marking.TryGetMarking(marking, out var proto))
                        continue;

                    if (proto.BodyPart == HumanoidVisualLayers.UndergarmentBottom)
                        hasBottom = true;
                    else if (proto.BodyPart == HumanoidVisualLayers.UndergarmentTop)
                        hasTop = true;
                }
            }
        }

        var body = (ent.Owner, hideable);
        _hideableLayers.SetLayerOcclusion(body, HumanoidVisualLayers.Genital, hasBottom, UndergarmentSource);
        _hideableLayers.SetLayerOcclusion(body, HumanoidVisualLayers.Penis, hasBottom, UndergarmentSource);
        _hideableLayers.SetLayerOcclusion(body, HumanoidVisualLayers.Breasts, hasTop, UndergarmentSource);
    }
}
