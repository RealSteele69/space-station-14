// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Robust.Shared.Network;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._ClawCommand.Clothing;

public sealed partial class ClothingGrantingSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingGrantComponentComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<ClothingGrantComponentComponent, GotUnequippedEvent>(OnUnequip);
    }

    private void OnEquip(Entity<ClothingGrantComponentComponent> ent, ref GotEquippedEvent args)
    {
        // Granted components are networked to the client, so only the server adds them.
        if (_net.IsClient)
            return;

        if (!TryComp<ClothingComponent>(ent, out var clothing) || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        foreach (var (name, data) in ent.Comp.Components)
        {
            var registration = _componentFactory.GetRegistration(name);
            if (HasComp(args.EquipTarget, registration.Type))
                continue;

            var component = (Component) _serialization.CreateCopy(data.Component, notNullableOverride: true);
            AddComp(args.EquipTarget, component, true);
            ent.Comp.Active.Add(name);
        }
    }

    private void OnUnequip(Entity<ClothingGrantComponentComponent> ent, ref GotUnequippedEvent args)
    {
        if (_net.IsClient)
            return;

        foreach (var name in ent.Comp.Active)
        {
            RemComp(args.EquipTarget, _componentFactory.GetRegistration(name).Type);
        }

        ent.Comp.Active.Clear();
    }
}
