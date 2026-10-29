using Content.Shared.Roles.Components;

namespace Content.Shared._ClawCommand.GangConversion.Roles;

/// <summary>
/// Flags a mind as a gang boss, picked at round start. Added to the mind role entity alongside
/// <c>MindRole</c>, same pattern as every other team antag in this fork.
/// </summary>
[RegisterComponent]
public sealed partial class GangBossRoleComponent : BaseMindRoleComponent;
