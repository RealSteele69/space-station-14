using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Mousetrap;

/// <summary>
///     Claw Command - This mob sets off armed mousetraps even when wearing shoes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AlwaysTriggerMousetrapComponent : Component;
