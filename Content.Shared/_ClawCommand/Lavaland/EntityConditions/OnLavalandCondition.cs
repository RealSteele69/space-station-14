// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Lavaland.EntityConditions;

/// <summary>
/// Passes only while the entity is on a Lavaland map.
/// </summary>
/// <inheritdoc cref="EntityCondition"/>
public sealed partial class OnLavalandCondition : EntityConditionBase<OnLavalandCondition>
{
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) =>
        Loc.GetString("entity-condition-guidebook-on-lavaland", ("onLavaland", !Inverted));
}
