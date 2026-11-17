using Robust.Shared.Console;
using Robust.Shared.Player;
using Content.Shared.Administration;
using Content.Server.Spawners.Components;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Preferences;
using Content.Server.Preferences.Managers;
using Content.Shared.Players;
using Content.Server.Administration.Managers;
using Content.Shared.Spawners.Components;
using Content.Shared.Station.Systems;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
namespace Content.Server._ClawCommand.Centcomm;

internal sealed partial class CentcommSystem : EntitySystem
{
    [Dependency] private IConsoleHost _consoleHost = default!;
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;

    private static readonly ProtoId<JobPrototype> CentcommOfficialJob = "CentralCommandOfficial";
    private static readonly ProtoId<JobPrototype> ClawCommandOfficerJob = "ClawCommandOfficer";
    private static readonly EntProtoId CentcommHeadset = "ClothingHeadsetAltCentCom";
    private static readonly EntProtoId CentcommPda = "CentcomPDA";
    private static readonly EntProtoId CentcommDeathRattle = "DeathRattleImplantCentcomm";

    public override void Initialize()
    {
        base.Initialize();

        _consoleHost.RegisterCommand("centcomm", Loc.GetString("centcomm-official-spawn-command-desc"), "centcomm [clawGear]",
            CentcommCallback, CentcommCompletion);

        _consoleHost.RegisterCommand("centcomm.officer", Loc.GetString("centcomm-spawn-command-desc"), "centcomm.officer",
            CentcommOfficerCallback);
    }

    private CompletionResult CentcommCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.Booleans, Loc.GetString("centcomm-spawn-command-arg-clawgear"))
            : CompletionResult.Empty;
    }

    [AnyCommand]
    public void CentcommCallback(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is null)
        {
            shell.WriteError("A player must execute this command.");
            return;
        }

        if (shell.Player is not ICommonSession player)
        {
            shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        var data = player.ContentData();
        if (data?.UserId == null)
        {
            shell.WriteError(Loc.GetString("shell-entity-is-not-mob"));
            return;
        }

        if (!_adminManager.HasAdminFlag(shell.Player, AdminFlags.VIPPlus)
            && !_adminManager.HasAdminFlag(shell.Player, AdminFlags.Admin))
        {
            shell.WriteError("You need to be a VIP Plus tier patron for access to this command.");
            return;
        }

        if (shell.Player.AttachedEntity is null)
        {
            shell.WriteError("You must be attached to an entity, observe as ghost.");
            return;
        }

        if (!_mindSystem.TryGetMind(shell.Player.AttachedEntity.Value, out var mindId, out _))
        {
            shell.WriteError("You must have a mind, try observe as ghost.");
            return;
        }

        var clawGear = false;
        if (args.Length > 0 && !bool.TryParse(args[0], out clawGear))
        {
            shell.WriteError(Loc.GetString("shell-invalid-bool"));
            return;
        }

        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (points.MoveNext(out _, out var spawnPoint, out var xform))
        {
            if (spawnPoint.Job?.Id != CentcommOfficialJob)
                continue;

            var character = (HumanoidCharacterProfile)_prefs.GetPreferences(data.UserId).SelectedCharacter;

            var mob = clawGear
                ? SpawnClawGearOfficial(xform.Coordinates, character)
                : _stationSpawning.SpawnPlayerMob(xform.Coordinates, CentcommOfficialJob, character, null);

            _mindSystem.TransferTo(mindId, mob);

            shell.WriteLine("Success.");
            return;
        }

        shell.WriteError("No CentralCommandOfficial spawn point found on the map.");
    }

    /// <summary>
    /// Spawns a CentComm Official wearing Claw Command Officer gear (job gear + their CCO loadout),
    /// but with a CentComm PDA/ID and headset.
    /// </summary>
    private EntityUid SpawnClawGearOfficial(EntityCoordinates coordinates, HumanoidCharacterProfile character)
    {
        var mob = _stationSpawning.SpawnPlayerMob(coordinates, ClawCommandOfficerJob, character, null);

        ReplaceSlot(mob, "ears", CentcommHeadset);
        ReplaceSlot(mob, "id", CentcommPda);

        var official = _prototype.Index(CentcommOfficialJob);
        _stationSpawning.SetPdaAndIdCardData(mob, Name(mob), official, null);

        // The CCO job already gives a mindshield; add the rest of the CentComm Official's implants.
        _implants.AddImplant(mob, CentcommDeathRattle);

        return mob;
    }

    private void ReplaceSlot(EntityUid mob, string slot, EntProtoId item)
    {
        if (_inventory.TryUnequip(mob, slot, out var removed, silent: true, force: true))
            QueueDel(removed);

        var spawned = Spawn(item, Transform(mob).Coordinates);
        if (!_inventory.TryEquip(mob, spawned, slot, silent: true, force: true))
            QueueDel(spawned);
    }

    [AnyCommand]
    public void CentcommOfficerCallback(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is null)
        {
            shell.WriteError("A player must execute this command.");
            return;
        }

        if (shell.Player is not ICommonSession player)
        {
            shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        var data = player.ContentData();
        if (data?.UserId == null)
        {
            shell.WriteError(Loc.GetString("shell-entity-is-not-mob"));
            return;
        }

        if (!_adminManager.HasAdminFlag(shell.Player, AdminFlags.VIPPlus)
            && !_adminManager.HasAdminFlag(shell.Player, AdminFlags.Admin))
        {
            shell.WriteError("You need to be a VIP Plus tier patron for access to this command.");
            return;
        }

        if (shell.Player.AttachedEntity is null)
        {
            shell.WriteError("You must be attached to an entity, observe as ghost.");
            return;
        }

        if (!_mindSystem.TryGetMind(shell.Player.AttachedEntity.Value, out var mindId, out _))
        {
            shell.WriteError("You must have a mind, try observe as ghost.");
            return;
        }

        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (points.MoveNext(out _, out var spawnPoint, out var xform))
        {
            if (spawnPoint.Job?.Id != "CentralCommandOfficial")
                continue;

            var character = (HumanoidCharacterProfile)_prefs.GetPreferences(data.UserId).SelectedCharacter;

            var mob = _entityManager.System<StationSpawningSystem>()
                .SpawnPlayerMob(xform.Coordinates, "CentralCommandOfficer", character, null);

            _mindSystem.TransferTo(mindId, mob);

            shell.WriteLine("Success.");
            return;
        }

        shell.WriteError("No CentralCommandOfficial spawn point found on the map.");
    }
}
