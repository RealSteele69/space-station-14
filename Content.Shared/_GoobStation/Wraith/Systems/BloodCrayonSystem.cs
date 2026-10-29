// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared._GoobStation.Wraith.Systems;

public sealed partial class BloodCrayonSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodWritingComponent, BloodWritingEvent>(OnBloodWritingAction);
    }

    private void OnBloodWritingAction(Entity<BloodWritingComponent> ent, ref BloodWritingEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.BloodCrayon == null)
        {
            _handsSystem.AddHand(ent.Owner, ent.Comp.HandName, HandLocation.Middle);

            var crayon = PredictedSpawnAtPosition(ent.Comp.BloodCrayonEntId, Transform(ent.Owner).Coordinates);
            _handsSystem.TryForcePickup(ent.Owner, crayon, ent.Comp.HandName, false);

            ent.Comp.BloodCrayon = crayon;
        }
        else
        {
            _handsSystem.RemoveHand(ent.Owner, ent.Comp.HandName);
            PredictedQueueDel(ent.Comp.BloodCrayon);
            ent.Comp.BloodCrayon = null;
        }
        Dirty(ent);

        args.Handled = true;
    }
}
