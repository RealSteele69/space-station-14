// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using System.Linq;
using Content.Shared._GoobStation.Wraith.Components;
using Content.Shared._GoobStation.Wraith.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._GoobStation.Wraith.Systems;

/// <summary>
/// This handles the command ability of Wraith.
/// Hurls a few nearby loose objects at the chosen target.
/// </summary>
public sealed partial class WraithCommandSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookupSystem = default!;
    [Dependency] private ThrowingSystem _throwingSystem = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _netManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WraithCommandComponent, WraithCommandEvent>(OnCommand);
    }

    private void OnCommand(Entity<WraithCommandComponent> ent, ref WraithCommandEvent args)
    {
        _stun.TryUpdateStunDuration(args.Target, ent.Comp.StunDuration);

        if (_netManager.IsClient)
            return;

        var found = new HashSet<Entity<PullableComponent>>();
        _lookupSystem.GetEntitiesInRange(Transform(ent.Owner).Coordinates, ent.Comp.SearchRange, found);
        var foundList = found.ToList();
        _random.Shuffle(foundList);

        foreach (var entity in foundList)
        {
            if (_whitelist.IsWhitelistPassOrNull(ent.Comp.Blacklist, entity))
                continue;

            _throwingSystem.TryThrow(entity, Transform(args.Target).Coordinates, ent.Comp.ThrowSpeed, ent.Owner);
        }

        args.Handled = true;
    }
}
