// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

namespace Content.Shared._GoobStation.SlaughterDemon.Items;

/// <summary>
/// This is used for tracking objectives
/// </summary>
[RegisterComponent]
public sealed partial class VialSummonComponent : Component
{
    /// <summary>
    ///  The entity who summoned an entity from the vial
    /// </summary>
    [DataField]
    public EntityUid? Summoner;

    /// <summary>
    ///  Ensures we get the objective only for that wizard.
    /// </summary>
    [DataField]
    public bool Used;
}

/// <summary>
/// Raised when a game rule gets added from an item (such as the slaughter demon summoning vials).
/// Adapted from Goobstation's Content.Goobstation.Shared.GameTicking.AddGameRuleItemEvent, which lived in a
/// shared "AddGameRuleOnUse" item-use subsystem this fork doesn't have; this fork instead raises it from the
/// <see cref="Content.Server._GoobStation.SlaughterDemon.Destructible.AddGameRuleBehavior"/> destructible
/// behavior, so it's kept small and scoped to SlaughterDemon rather than porting that whole subsystem.
/// </summary>
[ByRefEvent]
public record struct AddGameRuleItemEvent(EntityUid? Initiator);
