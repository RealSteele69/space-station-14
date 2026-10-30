using Content.Shared._ClawCommand.Mousetrap; // Claw Command
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Mousetrap; // Claw Command
using Content.Shared.StepTrigger.Components;

namespace Content.Shared.StepTrigger.Systems;

public sealed partial class StepTriggerImmuneSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PreventableStepTriggerComponent, StepTriggerAttemptEvent>(OnStepTriggerClothingAttempt);
        SubscribeLocalEvent<PreventableStepTriggerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnStepTriggerClothingAttempt(Entity<PreventableStepTriggerComponent> ent, ref StepTriggerAttemptEvent args)
    {
        // Claw Command - shoes don't save rodents from mousetraps.
        if (HasComp<MousetrapComponent>(ent) && HasComp<AlwaysTriggerMousetrapComponent>(args.Tripper))
            return;

        if (HasComp<ProtectedFromStepTriggersComponent>(args.Tripper) || _inventory.TryGetInventoryEntity<ProtectedFromStepTriggersComponent>(args.Tripper, out _))
        {
            args.Cancelled = true;
        }
    }

    private void OnExamined(EntityUid uid, PreventableStepTriggerComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("clothing-required-step-trigger-examine"));
    }
}
