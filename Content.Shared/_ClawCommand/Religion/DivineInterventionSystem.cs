// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._ClawCommand.Religion;

/// <summary>
/// Handles "spell denial": holy items that block unholy touch powers.
/// </summary>
public sealed partial class DivineInterventionSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;

    /// <summary>
    /// Is the target protected by a held or correctly worn holy item?
    /// </summary>
    public bool ShouldDeny(EntityUid target, out EntityUid? denyingItem)
    {
        denyingItem = null;
        var divineQuery = GetEntityQuery<DivineInterventionComponent>();

        foreach (var held in _hands.EnumerateHeld(target))
        {
            if (!divineQuery.HasComp(held))
                continue;

            denyingItem = held;
            return true;
        }

        var slots = _inventory.GetSlotEnumerator(target, SlotFlags.WITHOUT_POCKET);
        while (slots.NextItem(out var item, out var slot))
        {
            if (!divineQuery.TryComp(item, out var comp)
                || (slot.SlotFlags & comp.ValidSpellDenialSlots) == 0x0)
                continue;

            denyingItem = item;
            return true;
        }

        return false;
    }

    /// <summary>
    /// <see cref="ShouldDeny(EntityUid, out EntityUid?)"/>, playing the denial effects when it succeeds.
    /// </summary>
    public bool ShouldDeny(EntityUid target)
    {
        if (!ShouldDeny(target, out var denyingItem) || denyingItem is not { } item)
            return false;

        DenialEffects(item, target);
        return true;
    }

    private void DenialEffects(EntityUid item, EntityUid target)
    {
        if (_net.IsClient || !TryComp<DivineInterventionComponent>(item, out var comp))
            return;

        _popup.PopupEntity(Loc.GetString(comp.DenialString), target, PopupType.MediumCaution);
        _audio.PlayPvs(comp.DenialSound, target);
        Spawn(comp.EffectProto, Transform(target).Coordinates);
    }
}
