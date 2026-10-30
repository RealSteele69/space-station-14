using Content.Shared.Clothing;
using Content.Shared.Interaction.Components;

namespace Content.Shared._ClawCommand.Clothing;

/// <summary>
/// Handles <see cref="CursedClothingComponent"/>.
/// </summary>
public sealed class CursedClothingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursedClothingComponent, ClothingGotEquippedEvent>(OnEquipped);
    }

    private void OnEquipped(Entity<CursedClothingComponent> ent, ref ClothingGotEquippedEvent args)
    {
        EnsureComp<UnremoveableComponent>(ent);
    }
}
