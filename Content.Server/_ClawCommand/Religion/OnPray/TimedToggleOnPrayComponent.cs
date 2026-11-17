// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._ClawCommand.Religion.OnPray;

/// <summary>
/// Praying to this switches its <see cref="Content.Shared.Item.ItemToggle.Components.ItemToggleComponent"/> on
/// for <see cref="Duration"/>, then it switches itself back off. Its use delay, if any, is the cooldown between prayers.
/// </summary>
[RegisterComponent]
public sealed partial class TimedToggleOnPrayComponent : Component
{
    /// <summary>
    /// How long it stays on.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When the item switches itself off, if it was switched on by prayer.
    /// </summary>
    [ViewVariables]
    public TimeSpan? EndTime;
}
