// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
// Adapted: Goobstation raises AddGameRuleItemEvent from a shared "use item to add a game rule" subsystem
// this fork doesn't have. Here it's raised directly by the AddGameRuleBehavior destructible behavior (see
// Content.Server._GoobStation.SlaughterDemon.Destructible.AddGameRuleBehavior), but this system otherwise
// works identically to the source.

using Content.Shared._GoobStation.SlaughterDemon.Items;

namespace Content.Server._GoobStation.SlaughterDemon.Items;

/// <summary>
/// This handles attaching the wizard to the slaughter demon's objective
/// </summary>
public sealed class VialSummonSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VialSummonComponent, AddGameRuleItemEvent>(OnAddGameRule);
    }

    private void OnAddGameRule(Entity<VialSummonComponent> ent, ref AddGameRuleItemEvent args) =>
        ent.Comp.Summoner = args.Initiator;
}
