// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aineias1 <dmitri.s.kiselev@gmail.com>
// SPDX-FileCopyrightText: 2025 FaDeOkno <143940725+FaDeOkno@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 McBosserson <148172569+McBosserson@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Milon <plmilonpl@gmail.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 TheBorzoiMustConsume <197824988+TheBorzoiMustConsume@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Unlumination <144041835+Unlumy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 coderabbitai[bot] <136622811+coderabbitai[bot]@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 username <113782077+whateverusername0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 whateverusername0 <whateveremail>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Lavaland.Salvage;
using Content.Shared._ClawCommand.Lavaland.UnclaimedOre;
using Content.Shared.DragDrop;
using Content.Shared.Storage;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.Lavaland.OreBag;

public sealed partial class OreBagSystem : EntitySystem
{
    [Dependency] private SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private MiningPointsSystem _miningPoints = default!;
    [Dependency] private IGameTiming _timing = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<OreBagComponent, AfterInteractEvent>(OnAfterInteract);

        SubscribeLocalEvent<OreBagComponent, CanDropDraggedEvent>(OnDragAttempt);
        SubscribeLocalEvent<OreBagComponent, DragDropDraggedEvent>(OnDrag);
        SubscribeLocalEvent<OreBagComponent, CanDragEvent>(OnCanDragBox);
    }

    private void OnAfterInteract(EntityUid uid, OreBagComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach
          || args.Target == null
          || !HasComp<MiningPointsLatheComponent>(args.Target)
          || !_timing.IsFirstTimePredicted)
            return;

        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        var validEntities = new List<EntityUid>();

        foreach (var entity in storage.Container.ContainedEntities)
            if (HasComp<MaterialComponent>(entity))
                validEntities.Add(entity);

        foreach (var entity in validEntities)
            InsertOreAndAwardPoints(args.User, entity, args.Target.Value);
    }

    private void OnDrag(Entity<OreBagComponent> ent, ref DragDropDraggedEvent args)
    {
        if (!TryComp<StorageComponent>(ent.Owner, out var storage) || args.Handled)
            return;

        var validEntities = new List<EntityUid>();

        foreach (var entity in storage.Container.ContainedEntities)
            if (HasComp<MaterialComponent>(entity))
                validEntities.Add(entity);

        foreach (var entity in validEntities)
            InsertOreAndAwardPoints(args.User, entity, args.Target);

        args.Handled = true;
    }

    /// <summary>
    /// Inserts an ore bag's contents into a material storage entity, awarding mining points
    /// if the ore is unclaimed Lavaland ore and the target accepts mining points.
    /// </summary>
    private void InsertOreAndAwardPoints(EntityUid user, EntityUid ore, EntityUid target)
    {
        var points = TryComp<UnclaimedOreComponent>(ore, out var unclaimed)
            ? (uint) MathF.Round(unclaimed.MiningPoints)
            : 0;

        if (!_materialStorage.TryInsertMaterialEntity(user, ore, target))
            return;

        if (points > 0 && HasComp<MiningPointsLatheComponent>(target))
            _miningPoints.AddPoints(target, points);
    }

    private void OnDragAttempt(Entity<OreBagComponent> ent, ref CanDropDraggedEvent args)
    {
        if (HasComp<MiningPointsLatheComponent>(args.Target))
            args.CanDrop = true;
        args.Handled = true;
    }

    private void OnCanDragBox(Entity<OreBagComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }
}
