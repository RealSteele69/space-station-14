// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
// Originally Content.Goobstation.Server.Destructible.Thresholds.Behaviors.AddGameRuleBehavior. Scoped down
// to live alongside SlaughterDemon (the only current user, via BottleOfBlood/BottleOfTickles) rather than
// porting it as a general-purpose Destructible behavior, since nothing else in this fork needs it yet.

using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.GameTicking;
using Content.Shared._GoobStation.SlaughterDemon.Items;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Content.Shared.GameTicking;

namespace Content.Server._GoobStation.SlaughterDemon.Destructible;

[UsedImplicitly]
[DataDefinition]
public sealed partial class AddGameRuleBehavior : IThresholdBehavior
{
    [DataField(required: true)]
    public EntProtoId Rule;

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        var ev = new AddGameRuleItemEvent(cause);
        system.EntityManager.EventBus.RaiseLocalEvent(owner, ref ev);

        system.EntityManager.System<GameTicker>().StartGameRule(Rule);
    }
}
