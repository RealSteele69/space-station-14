// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._GoobStation.Wraith.Systems;

public sealed partial class SummonPlagueRatSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SummonPlagueRatComponent, SummonPlagueRatEvent>(OnSummonRat);
    }

    private void OnSummonRat(Entity<SummonPlagueRatComponent> ent, ref SummonPlagueRatEvent args)
    {
        var uid = ent.Owner;
        var comp = ent.Comp;
        var xform = Transform(uid);

        if (args.Handled)
            return;

        if (_physics.GetEntitiesIntersectingBody(ent.Owner, (int)CollisionGroup.Impassable).Count > 0)
        {
            _popup.PopupClient(Loc.GetString("wraith-plaguerat-blocked"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        if (_net.IsServer)
        {
            Spawn(comp.RatProto, xform.Coordinates);
            _popup.PopupEntity(Loc.GetString("wraith-plaguerat-channel"), uid, uid);
            _admin.Add(LogType.EntitySpawn, LogImpact.Medium, $"{args.Performer} has summoned a plague rat");
        }

        args.Handled = true;
    }
}
