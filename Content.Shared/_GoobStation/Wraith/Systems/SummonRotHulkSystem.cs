// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Network;

namespace Content.Shared._GoobStation.Wraith.Systems;

public sealed partial class SummonRotHulkSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedAdminLogManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SummonRotHulkComponent, SummonRotHulkEvent>(OnSummonHulk);
    }

    private void OnSummonHulk(Entity<SummonRotHulkComponent> ent, ref SummonRotHulkEvent args)
    {
        if (args.Handled)
            return;
        if (_net.IsClient)
            return;

        var uid = ent.Owner;
        var comp = ent.Comp;
        var xform = Transform(uid);

        var nearbyTrash = new List<EntityUid>();
        foreach (var e in _lookup.GetEntitiesInRange(xform.Coordinates, comp.SearchRadius, LookupFlags.Uncontained))
        {
            if (_tags.HasTag(e, comp.TrashTag))
                nearbyTrash.Add(e);
            if (nearbyTrash.Count >= comp.MaxTrash)
                break;
        }

        if (nearbyTrash.Count < comp.MinTrash)
        {
            _popup.PopupEntity(Loc.GetString("wraith-rot-hulk-too-clean"), ent.Owner, ent.Owner);
            return;
        }

        foreach (var trash in nearbyTrash)
            QueueDel(trash);

        // Choose which prototype to spawn
        var isBuff = nearbyTrash.Count >= comp.BuffThreshold;
        var proto = isBuff
            ? comp.BuffRotHulkProto
            : comp.RotHulkProto;

        Spawn(proto, xform.Coordinates);
        _popup.PopupEntity(Loc.GetString("wraith-rot-hulk-emerge"), ent.Owner, ent.Owner);
        _admin.Add(LogType.EntitySpawn, LogImpact.Medium, $"{args.Performer} has summoned a rot hulk");

        args.Handled = true;
    }
}
