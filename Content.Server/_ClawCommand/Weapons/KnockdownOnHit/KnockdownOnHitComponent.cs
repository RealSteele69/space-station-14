// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Weapons.KnockdownOnHit;

/// <summary>
/// Knocks living targets down when this melee weapon hits them.
/// If <see cref="UseDelayId"/> is set, the knockdown only happens while that use delay is ready, and resets it.
/// </summary>
[RegisterComponent]
public sealed partial class KnockdownOnHitComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    [DataField]
    public bool DropItems;

    [DataField]
    public bool AutoStand = true;

    [DataField]
    public bool RefreshDuration = true;

    [DataField]
    public bool KnockdownOnHeavyAttack = true;

    /// <summary>
    /// Use delay gating the knockdown. Null means every hit knocks down.
    /// </summary>
    [DataField]
    public string? UseDelayId;

    [DataField]
    public SoundSpecifier? KnockdownSound;

    /// <summary>
    /// Effect spawned on each knocked down target.
    /// </summary>
    [DataField]
    public EntProtoId? KnockdownEffect;
}
