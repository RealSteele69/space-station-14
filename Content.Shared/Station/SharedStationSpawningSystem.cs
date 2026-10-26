using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Station;

public abstract partial class SharedStationSpawningSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] protected InventorySystem InventorySystem = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;

    [Dependency] private EntityQuery<HandsComponent> _handsQuery = default!;
    [Dependency] private EntityQuery<InventoryComponent> _inventoryQuery = default!;
    [Dependency] private EntityQuery<StorageComponent> _storageQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    /// <summary>
    ///     Equips the data from a `RoleLoadout` onto an entity.
    /// </summary>
    public void EquipRoleLoadout(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto)
    {
        // Order loadout selections by the order they appear on the prototype.
        foreach (var group in loadout.SelectedLoadouts.OrderBy(x => roleProto.Groups.FindIndex(e => e == x.Key)))
        {
            foreach (var items in group.Value)
            {
                if (!ProtoMan.TryIndex(items.Prototype, out var loadoutProto))
                {
                    Log.Error($"Unable to find loadout prototype for {items.Prototype}");
                    continue;
                }

                EquipStartingGear(entity, loadoutProto, raiseEvent: false, customName: items.CustomName); // Claw Command
            }
        }

        EquipRoleName(entity, loadout, roleProto);
    }

    /// <summary>
    /// Applies the role's name as applicable to the entity.
    /// </summary>
    public void EquipRoleName(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto)
    {
        string? name = null;

        if (roleProto.CanCustomizeName)
        {
            name = loadout.EntityName;
        }

        if (string.IsNullOrEmpty(name) && ProtoMan.Resolve(roleProto.NameDataset, out var nameData))
        {
            name = Loc.GetString(_random.Pick(nameData.Values));
        }

        if (!string.IsNullOrEmpty(name))
        {
            _metadata.SetEntityName(entity, name);
        }
    }

    public void EquipStartingGear(EntityUid entity, LoadoutPrototype loadout, bool raiseEvent = true, string? customName = null) // Claw Command - customName
    {
        EquipStartingGear(entity, loadout.StartingGear, raiseEvent, customName);
        EquipStartingGear(entity, (IEquipmentLoadout) loadout, raiseEvent, customName);
    }

    /// <summary>
    /// CLAW COMMAND: Added custom names.
    /// <see cref="EquipStartingGear(Robust.Shared.GameObjects.EntityUid,System.Nullable{Robust.Shared.Prototypes.ProtoId{Content.Shared.Roles.StartingGearPrototype}},bool)"/>
    /// </summary>
    public void EquipStartingGear(EntityUid entity, ProtoId<StartingGearPrototype>? startingGear, bool raiseEvent = true, string? customName = null)
    {
        ProtoMan.Resolve(startingGear, out var gearProto);
        EquipStartingGear(entity, gearProto, raiseEvent, customName);
    }

    /// <summary>
    /// <see cref="EquipStartingGear(Robust.Shared.GameObjects.EntityUid,System.Nullable{Robust.Shared.Prototypes.ProtoId{Content.Shared.Roles.StartingGearPrototype}},bool)"/>
    /// </summary>
    public void EquipStartingGear(EntityUid entity, StartingGearPrototype? startingGear, bool raiseEvent = true, string? customName = null)
    {
        EquipStartingGear(entity, (IEquipmentLoadout?) startingGear, raiseEvent, customName);
    }

    /// <summary>
    /// Equips starting gear onto the given entity.
    /// </summary>
    /// <param name="entity">Entity to load out.</param>
    /// <param name="startingGear">Starting gear to use.</param>
    /// <param name="raiseEvent">Should we raise the event for equipped. Set to false if you will call this manually</param>
    public void EquipStartingGear(EntityUid entity, IEquipmentLoadout? startingGear, bool raiseEvent = true, string? customName = null) // Claw Command - customName
    {
        if (startingGear == null)
            return;

        var xform = _xformQuery.GetComponent(entity);

        if (InventorySystem.TryGetSlots(entity, out var slotDefinitions))
        {
            foreach (var slot in slotDefinitions)
            {
                var equipmentStr = startingGear.GetGear(slot.Name);
                if (!string.IsNullOrEmpty(equipmentStr))
                {
                    var equipmentEntity = Spawn(equipmentStr, xform.Coordinates);

                    if (!string.IsNullOrEmpty(customName)) // Claw Command
                        _metadata.SetEntityName(equipmentEntity, customName);

                    InventorySystem.TryEquip(entity, equipmentEntity, slot.Name, silent: true, force: true);
                }
            }
        }

        if (_handsQuery.TryComp(entity, out var handsComponent))
        {
            var inhand = startingGear.Inhand;
            var coords = xform.Coordinates;
            foreach (var prototype in inhand)
            {
                var inhandEntity = Spawn(prototype, coords);

                if (!string.IsNullOrEmpty(customName)) // Claw Command
                    _metadata.SetEntityName(inhandEntity, customName);

                if (_handsSystem.TryGetEmptyHand((entity, handsComponent), out var emptyHand) &&
                    _handsSystem.TryPickup(entity, inhandEntity, emptyHand, checkActionBlocker: false, handsComp: handsComponent))
                {
                    continue;
                }

                // Claw Command - no free hand to take it: try stowing it somewhere instead of
                // just leaving it unclaimed at our feet with no record of why.
                if (!TryStowOverflowGear(entity, inhandEntity))
                    Log.Warning($"Starting gear item {prototype} had no free hand or storage space on {ToPrettyString(entity)} and was left on the floor.");
            }
        }

        if (startingGear.Storage.Count > 0)
        {
            var coords = _xformSystem.GetMapCoordinates(entity);
            _inventoryQuery.TryComp(entity, out var inventoryComp);

            foreach (var (slotName, entProtos) in startingGear.Storage)
            {
                if (entProtos == null || entProtos.Count == 0)
                    continue;

                if (inventoryComp != null &&
                    InventorySystem.TryGetSlotEntity(entity, slotName, out var slotEnt, inventoryComponent: inventoryComp) &&
                    _storageQuery.TryComp(slotEnt, out var storage))
                {

                    foreach (var entProto in entProtos)
                    {
                        var spawnedEntity = Spawn(entProto, coords);

                        if (!string.IsNullOrEmpty(customName)) // Claw Command
                            _metadata.SetEntityName(spawnedEntity, customName);

                        if (_storage.Insert(slotEnt.Value, spawnedEntity, out _, storageComp: storage, playSound: false))
                            continue;

                        // Claw Command - the intended slot (e.g. a full backpack) had no room left.
                        // Try a free hand or any other equipped storage before giving up on it.
                        if (!TryStowOverflowGear(entity, spawnedEntity))
                            Log.Warning($"Loadout item {entProto} for slot \"{slotName}\" didn't fit anywhere on {ToPrettyString(entity)} and was left on the floor.");
                    }
                }
            }
        }

        if (raiseEvent)
        {
            var ev = new StartingGearEquippedEvent(entity);
            RaiseLocalEvent(entity, ref ev);
        }
    }

    /// <summary>
    /// Claw Command - last-resort placement for a piece of starting/loadout gear that didn't fit in its
    /// intended spot (usually a full backpack): tries any free hand, then any other equipped container
    /// with room, before giving up. Returns false if neither worked, in which case the item is left where
    /// it was spawned (on the ground at the entity's feet) instead of silently vanishing into nowhere.
    /// </summary>
    private bool TryStowOverflowGear(EntityUid entity, EntityUid item)
    {
        if (_handsQuery.TryComp(entity, out var hands) &&
            _handsSystem.TryGetEmptyHand((entity, hands), out var emptyHand) &&
            _handsSystem.TryPickup(entity, item, emptyHand, checkActionBlocker: false, handsComp: hands))
        {
            return true;
        }

        if (InventorySystem.TryGetSlots(entity, out var slotDefinitions))
        {
            foreach (var slot in slotDefinitions)
            {
                if (!InventorySystem.TryGetSlotEntity(entity, slot.Name, out var slotEnt) ||
                    !_storageQuery.TryComp(slotEnt, out var storage))
                {
                    continue;
                }

                if (_storage.Insert(slotEnt.Value, item, out _, storageComp: storage, playSound: false))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets all the gear for a given slot when passed a loadout.
    /// </summary>
    /// <param name="loadout">The loadout to look through.</param>
    /// <param name="slot">The slot that you want the clothing for.</param>
    /// <returns>
    ///     If there is a value for the given slot, it will return the proto id for that slot.
    ///     If nothing was found, will return null
    /// </returns>
    public string? GetGearForSlot(RoleLoadout? loadout, string slot)
    {
        if (loadout == null)
            return null;

        foreach (var group in loadout.SelectedLoadouts)
        {
            foreach (var items in group.Value)
            {
                if (!ProtoMan.Resolve(items.Prototype, out var loadoutPrototype))
                    return null;

                var gear = ((IEquipmentLoadout) loadoutPrototype).GetGear(slot);
                if (gear != string.Empty)
                    return gear;
            }
        }

        return null;
    }
}
