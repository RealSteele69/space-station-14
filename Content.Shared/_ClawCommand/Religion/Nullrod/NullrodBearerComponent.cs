// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._ClawCommand.Religion.Nullrod;

/// <summary>
/// Added to a bible user once they bind a null rod, so they can recall it at an altar.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NullrodBearerComponent : Component
{
    /// <summary>
    /// The bound null rod.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? NullRod;
}
