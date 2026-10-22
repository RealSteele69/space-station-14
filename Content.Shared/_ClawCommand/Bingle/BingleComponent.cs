// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Bingle;

/// <summary>
/// A bingle: a minion that feeds its pit. The prime bingle digs the pit when it spawns.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BingleComponent : Component
{
    /// <summary>
    /// Whether this bingle has already been granted the upgrade polymorph by its pit.
    /// </summary>
    [DataField]
    public bool Upgraded;

    /// <summary>
    /// The prime bingle digs a new pit where it spawns instead of joining an existing one.
    /// </summary>
    [DataField]
    public bool Prime;

    [DataField]
    public EntityUid? MyPit;
}

[Serializable, NetSerializable]
public enum BingleVisual : byte
{
    Upgraded,
    Combat,
}
