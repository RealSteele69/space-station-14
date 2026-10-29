using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.GangConversion.Components;

/// <summary>
/// Marks the founder and leader of a gang. Only a boss can promote a fellow member to enforcer.
/// Always accompanied by <see cref="GangMemberComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GangBossComponent : Component;
