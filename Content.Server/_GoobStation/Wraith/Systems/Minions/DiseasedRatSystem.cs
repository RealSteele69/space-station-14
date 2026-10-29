// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: dropped ComponentsToTransfer - this fork's PolymorphConfiguration has no such field (see
// DiseasedRatComponent.cs). PolymorphEntity already transfers name/damage on its own.

using Content.Shared._GoobStation.Wraith.Components.Mobs;
using Content.Shared._GoobStation.Wraith.Minions.Plaguebringer;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server._GoobStation.Wraith.Systems.Minions;

public sealed partial class DiseasedRatSystem : SharedDiseasedRatSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;

    protected override void Evolve(EntityUid uid, ProtoId<DiseasedRatFormUnlockPrototype> newProto)
    {
        base.Evolve(uid, newProto);

        if (!_proto.TryIndex(newProto, out var index)
            || index.Entity == null)
            return;

        var poly = new PolymorphConfiguration
        {
            Entity = index.Entity.Value,
            TransferName = true,
            TransferDamage = true,
            Forced = true,
            RevertOnCrit = false,
            RevertOnDeath = false,
            AllowRepeatedMorphs = true,
        };

        _polymorph.PolymorphEntity(uid, poly);
    }
}
