// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Bingle;

/// <summary>
/// A pit that swallows whatever falls in and turns it into new bingles.
/// </summary>
[RegisterComponent]
public sealed partial class BinglePitComponent : Component
{
    /// <summary>
    /// Points stored towards the next bingle.
    /// </summary>
    [DataField]
    public float BinglePoints;

    /// <summary>
    /// Points for anything that falls in while alive or critical.
    /// </summary>
    [DataField]
    public float PointsForAlive = 5f;

    /// <summary>
    /// Extra points for a humanoid, as a fraction of the cost of one bingle at the current level.
    /// </summary>
    [DataField]
    public float HumanoidBingleFraction = 0.5f;

    /// <summary>
    /// Points needed for a new bingle, multiplied by the pit level.
    /// </summary>
    [DataField]
    public float SpawnNewAt = 20f;

    /// <summary>
    /// Bingles made since the last level up.
    /// </summary>
    [DataField]
    public float MinionsMade;

    /// <summary>
    /// Bingles needed for the pit to level up.
    /// </summary>
    [DataField]
    public float UpgradeMinionsAfter = 10f;

    /// <summary>
    /// The most living bingles this pit will keep at once. Points keep building while at the cap.
    /// </summary>
    [DataField]
    public int MaxBingles = 6;

    [DataField]
    public float Level = 1f;

    /// <summary>
    /// The pit stops growing visually past this level.
    /// </summary>
    [DataField]
    public float MaxSize = 3f;

    /// <summary>
    /// Where swallowed entities go. Emptied when the pit is destroyed.
    /// </summary>
    [ViewVariables]
    public Container? Pit;

    [DataField]
    public SoundSpecifier FallingSound = new SoundPathSpecifier("/Audio/Effects/falling.ogg");

    [DataField]
    public EntProtoId GhostRoleToSpawn = "SpawnPointGhostBingle";

    [DataField]
    public string FloorTile = "FloorBingle";
}
