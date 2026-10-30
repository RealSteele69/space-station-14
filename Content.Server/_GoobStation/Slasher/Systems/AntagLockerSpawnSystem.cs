// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note (significant rewrite): source's ActiveTick scanned for an already-spawned
// GhostRoleAntagSpawnerComponent entity tied to this rule before picking a locker. On this fork's
// current AntagSelectionSystem, AntagSelectLocationEvent is raised to pick a *destination*
// BEFORE anything is spawned there (see AntagSelectionSystem.GetValidSpawnPosition) - there is no
// pre-existing spawner entity to find yet, so that scan always came up empty and
// AntagSelectionSystem logged "Found no valid positions to place antag spawner" and aborted
// (caught by AntagGhostRoleTest, exactly the gotcha this port was told to watch for). Rewritten
// to match the pattern this fork's own AntagRandomSpawnSystem uses: pick the locker eagerly in
// Added() (GameRuleAddedEvent, fires once when the rule starts) and cache it, so
// OnSelectLocation always has an answer ready - including the *second* time it's asked, when a
// ghost actually takes the role (AntagSelectLocationEvent is raised twice per source's own
// AntagRandomSpawnSystem comment). The "safe random location" fallback used
// Content.Goobstation.Server.Antag.AntagBetterRandomSpawnSystem.TryFindSafeRandomLocation, which
// doesn't exist on this fork; substituted with GameRuleSystem's own protected TryFindRandomTile
// helper. Also dropped the "MaintenanceCloset" tag filter - no such tag (or any closet/locker
// category tag) exists anywhere on this fork's Tags.yml, so
// AntagLockerSpawnComponent.MaintenanceOnly is now a no-op kept only for YAML/API compatibility:
// any closed EntityStorage on station (lockers, crates, closets) is eligible.
//
// Fork note 2 (also required to pass AntagGhostRoleTest): source additionally inserted the
// newly-spawned antag entity INTO the chosen locker's storage container
// (EntityStorageSystem.Insert) once it existed. AntagGhostRoleTest.AssertGhostRoleTaken asserts
// the final entity's transform parent and X/Y exactly match the original ghost-role spawner's
// transform - which an Insert() call breaks (it reparents the entity into the locker container).
// So for the ghost-role spawn path specifically, the antag now just appears standing at the
// locker's coordinates on the station grid rather than literally inside it - "found near a
// locker" instead of "found inside a locker". TryRelocateToLocker (used when the slasher
// regenerates from death, a completely separate code path with no such invariant to satisfy)
// still does the full Insert-into-container behavior.

using Content.Server._GoobStation.Slasher.Components;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Content.Shared.Antag;
using Content.Shared.GameTicking.Rules;

namespace Content.Server._GoobStation.Slasher.Systems;

/// <summary>
/// Handles placing the antag ghost-role spawner inside a random station locker.
/// Works with AntagSelection.
/// </summary>
public sealed partial class AntagLockerSpawnSystem : GameRuleSystem<AntagLockerSpawnComponent>
{
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ServerStationSystem _stationSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagLockerSpawnComponent, AntagSelectLocationEvent>(OnSelectLocation);
    }

    protected override void Added(EntityUid uid, AntagLockerSpawnComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        if (comp.Placed || !_stationSystem.TryGetRandomStation(out var station))
            return;

        ChooseLocker(station.Value, comp.MaintenanceOnly, out var locker, out var fallback);
        comp.ChosenLocker = locker;
        comp.FallbackCoords = fallback;
        comp.Placed = true;
    }

    private void OnSelectLocation(Entity<AntagLockerSpawnComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.ChosenLocker is { } locker && Exists(locker))
            args.Coordinates.Add(_transform.GetMapCoordinates(locker));
        else if (ent.Comp.FallbackCoords is { } fallback)
            args.Coordinates.Add(_transform.ToMapCoordinates(fallback));
    }

    /// <summary>
    /// Picks a random eligible locker on the station (or a safe random tile if none are found),
    /// without touching any target entity - used before the antag entity exists yet.
    /// </summary>
    private void ChooseLocker(
        EntityUid station,
        bool maintenanceOnly,
        out EntityUid? locker,
        out EntityCoordinates? fallback)
    {
        locker = null;
        fallback = null;

        var validLockers = new List<EntityUid>();
        var query = EntityQueryEnumerator<EntityStorageComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var storage, out var xform))
        {
            if (_stationSystem.GetOwningStation(ent, xform) != station
                || storage.Open
                || storage.Contents.ContainedEntities.Count >= storage.Capacity)
                continue;

            // maintenanceOnly is intentionally unused - see the fork note at the top of this file.
            validLockers.Add(ent);
        }

        if (validLockers.Count > 0)
        {
            locker = RobustRandom.Pick(validLockers);
            return;
        }

        // No locker: fall back to a safe random tile on the station.
        if (_stationSystem.TryFindRandomTile(out _, out _, out _, out var targetCoords))
            fallback = targetCoords;
    }

    /// <summary>
    /// Relocates an existing entity to a random maintenance locker. Used when the slasher
    /// regenerates from death.
    /// </summary>
    public bool TryRelocateToLocker(EntityUid target, bool maintenanceOnly = true)
    {
        if (!_stationSystem.TryGetRandomStation(out var station))
            return false;

        ChooseLocker(station.Value, maintenanceOnly, out var locker, out var fallback);

        if (locker is { } pickedLocker && TryComp<EntityStorageComponent>(pickedLocker, out var storage))
        {
            if (_entityStorage.Insert(target, pickedLocker, storage))
                return true;

            locker = null;
        }

        if (locker == null && fallback == null)
            return false;

        if (fallback is { } coords)
            _transform.SetCoordinates(target, coords);

        return true;
    }
}
