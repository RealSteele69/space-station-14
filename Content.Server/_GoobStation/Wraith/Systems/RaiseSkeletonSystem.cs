// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source gibbed via Content.Server.Body.Systems.BodySystem.GibBody, which doesn't exist here;
// substituted with Content.Shared.Gibbing.GibbingSystem.Gib.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Server.Popups;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Gibbing;
using Content.Shared.Mobs.Systems;
using Content.Shared.Storage.Components;

namespace Content.Server._GoobStation.Wraith.Systems;

public sealed partial class RaiseSkeletonSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private EntityStorageSystem _entityStorage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RaiseSkeletonComponent, RaiseSkeletonEvent>(OnRaiseSkeleton);
    }

    private void OnRaiseSkeleton(Entity<RaiseSkeletonComponent> ent, ref RaiseSkeletonEvent args)
    {
        // check if we targeted a locker, early return and deploy skeleton if yes
        if (TryComp<EntityStorageComponent>(args.Target, out var entStorage))
        {
            var skeleton = Spawn(ent.Comp.SkeletonProto, Transform(args.Target).Coordinates);

            if (!_entityStorage.Insert(skeleton, args.Target, entStorage))
            {
                Del(skeleton);
                return;
            }

            args.Handled = true;
            return;
        }

        // otherwise, check if target is dead
        if (!_mobState.IsDead(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("wraith-raise-no-corpse"), ent.Owner, ent.Owner);
            return;
        }

        // or rotting
        if (!_rotting.IsRotten(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("wraith-raise-body-refuse"), ent.Owner, ent.Owner);
            return;
        }

        // since both conditions passed, deploy the skeleton and gib them
        Spawn(ent.Comp.SkeletonProto, Transform(args.Target).Coordinates);
        _gibbing.Gib(args.Target);

        args.Handled = true;
    }
}
