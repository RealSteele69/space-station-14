// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._ClawCommand.CosmicCult;

/// <summary>
/// Cosmic Cult tuning cvars.
/// </summary>
[CVarDefs]
public sealed class CosmicCultCVars
{
    /// <summary>
    /// The multiplier for the difficulty of the monument.
    /// </summary>
    public static readonly CVarDef<float> CosmicCultistDifficultyMultiplier =
        CVarDef.Create("cosmiccult.difficulty_multiplier", 1.5f, CVar.SERVER);

    /// <summary>
    /// How much entropy a convert is worth towards the next monument tier.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultistEntropyValue =
        CVarDef.Create("cosmiccult.cultist_entropy_value", 5, CVar.SERVER);

    /// <summary>
    /// How much of the crew the cult is aiming to convert for a tier 3 monument.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultTargetConversionPercent =
        CVarDef.Create("cosmiccult.target_conversion_percent", 50, CVar.SERVER); // Claw Command: 40 -> 50

    /// <summary>
    /// How long the timer for the cult's stewardship vote lasts.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultStewardVoteTimer =
        CVarDef.Create("cosmiccult.steward_vote_timer", 80, CVar.SERVER);

    /// <summary>
    /// How long we wait before starting the stewardship vote.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultStewardVoteDelayTimer =
        CVarDef.Create("cosmiccult.steward_vote_delay", 25, CVar.SERVER);

    /// <summary>
    /// The delay between the monument getting upgraded to tier 2 and rifts starting to appear. the monument cannot be upgraded again in this time.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultT2RevealDelaySeconds =
        CVarDef.Create("cosmiccult.t2_reveal_delay_seconds", 30, CVar.SERVER);

    /// <summary>
    /// The delay between the monument getting upgraded to tier 3 and the crew learning of that fact. the monument cannot be upgraded again in this time.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultT3RevealDelaySeconds =
        CVarDef.Create("cosmiccult.t3_reveal_delay_seconds", 90, CVar.SERVER); // Claw Command: crew is warned sooner (180 -> 90)

    /// <summary>
    /// The delay between the monument getting upgraded to tier 3 and the finale starting.
    /// </summary>
    public static readonly CVarDef<int> CosmicCultFinaleDelaySeconds =
        CVarDef.Create("cosmiccult.extra_entropy_for_finale", 1, CVar.SERVER);
}
