using Content.Shared.Roles.Components;

namespace Content.Shared._ClawCommand.GangConversion.Roles;

/// <summary>
/// Flags a mind as a rank-and-file gang member (ganger or enforcer), granted on forced conversion.
/// Added to the mind role entity alongside <c>MindRole</c>, same pattern as Revolutionary's regular
/// <c>RevolutionaryRoleComponent</c> versus its head-rev counterpart.
/// </summary>
[RegisterComponent]
public sealed partial class GangMemberRoleComponent : BaseMindRoleComponent;
