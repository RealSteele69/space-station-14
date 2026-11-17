// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Religion.Nullrod;

/// <summary>
/// Altars with this let a chaplain pray to summon their bound null rod back to their hands.
/// </summary>
[RegisterComponent]
public sealed partial class RecallPrayableComponent : Component
{
    /// <summary>
    /// How long does the recall do-after take to complete.
    /// </summary>
    [DataField]
    public TimeSpan DoAfterDuration = TimeSpan.FromSeconds(5);
}
