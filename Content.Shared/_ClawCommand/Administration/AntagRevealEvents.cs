using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Administration;

/// <summary>
/// Sent by an admin client to reveal a player's antagonist status for the rest of the round.
/// Until revealed, the server masks antag status out of everything it sends to admins.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestAntagRevealEvent : EntityEventArgs
{
    public NetUserId Target;

    public RequestAntagRevealEvent(NetUserId target)
    {
        Target = target;
    }
}
