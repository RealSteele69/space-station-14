// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._ClawCommand.Lavaland.Procedural.Components;
using Content.Shared._ClawCommand.Lavaland.EntityConditions;
using Content.Shared.EntityConditions;

namespace Content.Server._ClawCommand.Lavaland.EntityConditions;

public sealed partial class OnLavalandConditionSystem : EntityConditionSystem<TransformComponent, OnLavalandCondition>
{
    protected override void Condition(Entity<TransformComponent> entity, ref EntityConditionEvent<OnLavalandCondition> args)
    {
        args.Result = HasComp<LavalandMapComponent>(entity.Comp.MapUid);
    }
}
