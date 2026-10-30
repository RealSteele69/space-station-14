using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Clothing;

/// <summary>
/// Marks clothing as becoming permanently stuck to the wearer once actually worn.
/// Unlike slapping <c>UnremoveableComponent</c> straight into the prototype, this only
/// locks the item down on <see cref="Content.Shared.Clothing.ClothingGotEquippedEvent"/>,
/// so it can still be picked up, handed off, or dropped normally while sitting in a hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CursedClothingComponent : Component
{
}
