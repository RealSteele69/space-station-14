using Robust.Shared.Configuration;

namespace Content.Shared._ClawCommand.CCVar;

public sealed partial class ClawCCVars
{
    /// <summary>
    /// Whether the preset and map votes start on their own every time the lobby opens.
    /// </summary>
    public static readonly CVarDef<bool> VoteAutoLobbyVotes =
        CVarDef.Create("vote.auto_lobby_votes", true, CVar.SERVERONLY);
}
