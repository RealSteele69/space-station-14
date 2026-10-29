using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.GangConversion.Components;

/// <summary>
/// Marks a member the boss has promoted. Enforcers get their own tattoo gun so they can recruit too.
/// Always accompanied by <see cref="GangMemberComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GangEnforcerComponent : Component;
