using Content.Shared.Roles;
using Content.Shared.Roles.Components;

namespace Content.Shared._ClawCommand.Blob.Roles;

/// <summary>
/// Mind role of a blob carrier or the blob overmind.
/// </summary>
[RegisterComponent]
public sealed partial class BlobRoleComponent : BaseMindRoleComponent
{

}

/// <summary>
/// Mind role of a crewmember raised by a blob spore. Removed again when the spore comes off.
/// </summary>
[RegisterComponent]
public sealed partial class BlobZombieRoleComponent : BaseMindRoleComponent
{

}
