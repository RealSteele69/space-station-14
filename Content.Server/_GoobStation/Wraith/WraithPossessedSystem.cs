// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared._GoobStation.Wraith.Components;
using Content.Server.Ghost.Roles.Components;

namespace Content.Server._GoobStation.Wraith;

/// <summary>
/// Hides a possessed ghost-role entity from the ghost-role takeover list while it's occupied, and restores
/// it once the possession ends - stops someone else taking over mid-possession.
/// </summary>
public sealed class WraithPossessedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostRoleComponent, PossessionStartedEvent>(OnPossessionStarted);
        SubscribeLocalEvent<GhostRoleComponent, PossessionEndedEvent>(OnPossessionEnded);
    }

    private void OnPossessionStarted(Entity<GhostRoleComponent> ent, ref PossessionStartedEvent args) =>
        RemComp<GhostTakeoverAvailableComponent>(ent.Owner);

    private void OnPossessionEnded(Entity<GhostRoleComponent> ent, ref PossessionEndedEvent args) =>
        EnsureComp<GhostTakeoverAvailableComponent>(ent.Owner);
}
