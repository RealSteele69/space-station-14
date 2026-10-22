#nullable enable
using Content.Shared._ClawCommand.CCVar;
using Content.Shared.CCVar;

namespace Content.IntegrationTests;

// Partial class containing test cvars
// This could probably be merged into the main file, but I'm keeping it separate to reduce
// conflicts for forks.
public static partial class PoolManager
{
    public static readonly (string cvar, string value)[] TestCvars =
    {
        // @formatter:off
        (CCVars.DatabaseSynchronous.Name,     "true"),
        (CCVars.DatabaseSnapshot.Name,        "true"),
        (CCVars.DatabaseSqliteDelay.Name,     "0"),
        (CCVars.HolidaysEnabled.Name,         "false"),
        (CCVars.GameMap.Name,                 TestMap),
        (CCVars.AdminLogsQueueSendDelay.Name, "0"),
        (CCVars.NPCMaxUpdates.Name,           "999999"),
        (CCVars.GameRoleTimers.Name,          "false"),
        (CCVars.GameRoleLoadoutTimers.Name,   "false"),
        (CCVars.GameRoleWhitelist.Name,       "false"),
        (CCVars.GridFill.Name,                "false"),
        (CCVars.PreloadGrids.Name,            "false"),
        (CCVars.ArrivalsShuttles.Name,        "false"),
        (CCVars.EmergencyShuttleEnabled.Name, "false"),
        (CCVars.ProcgenPreload.Name,          "false"),
        (CCVars.GatewayGeneratorEnabled.Name, "false"),
        (CCVars.GameDummyTicker.Name, "true"),
        (CCVars.GameLobbyEnabled.Name, "false"),
        (CCVars.ConfigPresetDevelopment.Name, "false"),
        (CCVars.AdminLogsEnabled.Name, "false"),
        (CCVars.AutosaveEnabled.Name, "false"),
        (CCVars.InteractionRateLimitCount.Name, "9999999"),
        (CCVars.InteractionRateLimitPeriod.Name, "0.1"),
        (CCVars.MovementMobPushing.Name, "false"),
        (CCVars.ResourceUploadingStoreDeletionDays.Name, "0"),
        (ClawCCVars.VoteAutoLobbyVotes.Name, "false"), // Claw Command - dummy test sessions can't receive vote updates
        (CCVars.WalkByDefault.Name, "false"), // Claw Command - fork defaults to walking; movement tests assume upstream run speed
        (CCVars.MoodIncreasesSpeed.Name, "false"), // Claw Command - a fed, fresh mob starts above neutral mood, which speeds it up
        (CCVars.MoodDecreasesSpeed.Name, "false"), // Claw Command - keep mood out of movement-speed assertions
        (CCVars.SpaceWind.Name, "false"), // Claw Command - the ported space wind throws mobs while freshly built test tiles settle
    };
}
