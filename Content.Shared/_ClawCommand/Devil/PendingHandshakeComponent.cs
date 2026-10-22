// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Devil;

/// <summary>
/// Added to someone a devil has offered a handshake to, until it is accepted or expires.
/// </summary>
[RegisterComponent]
public sealed partial class PendingHandshakeComponent : Component
{
    [DataField]
    public EntityUid? Offerer;

    [DataField]
    public TimeSpan ExpiryTime;
}
