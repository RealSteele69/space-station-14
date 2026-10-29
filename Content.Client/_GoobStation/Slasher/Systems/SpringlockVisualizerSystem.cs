// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Client.Clothing;
using Content.Shared._GoobStation.Slasher.Components;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Robust.Client.GameObjects;

namespace Content.Client._GoobStation.Slasher.Systems;

public sealed partial class SpringlockVisualizerSystem : VisualizerSystem<SpringlockClothingComponent>
{
    [Dependency] private SharedItemSystem _itemSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpringlockClothingComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: new[] { typeof(ClientClothingSystem) });
    }

    protected override void OnAppearanceChange(EntityUid uid, SpringlockClothingComponent component, ref AppearanceChangeEvent args)
    {
        _itemSystem.VisualsChanged(uid);
    }

    private void OnGetEquipmentVisuals(Entity<SpringlockClothingComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!ent.Comp.IsLocked || !ent.Comp.LockedClothingVisuals.TryGetValue(args.Slot, out var layers))
            return;

        var slotPrefix = $"{args.Slot}-";
        for (var i = args.Layers.Count - 1; i >= 0; i--)
        {
            if (args.Layers[i].Item1.StartsWith(slotPrefix))
                args.Layers.RemoveAt(i);
        }

        foreach (var layer in layers)
            args.Layers.Add(($"{args.Slot}-locked", layer));
    }
}
