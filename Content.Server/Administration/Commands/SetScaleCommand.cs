using System.Numerics;
using Content.Shared.Administration;
using Content.Shared.Sprite;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.VarEdit)]
public sealed partial class SetScaleCommand : LocalizedCommands
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "setscale";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine(Loc.GetString("cmd-setscale-help"));
            return;
        }

        if (!_playerManager.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
            return;
        }

        if (session.AttachedEntity is not { Valid: true } target)
        {
            shell.WriteError(Loc.GetString("cmd-setscale-no-entity"));
            return;
        }

        if (!float.TryParse(args[1], out var scale) || scale <= 0f)
        {
            shell.WriteError(Loc.GetString("cmd-setscale-invalid-scale"));
            return;
        }

        _entities.EntitySysManager.GetEntitySystem<SharedScaleVisualsSystem>().SetSpriteScale(target, new Vector2(scale, scale), pinBottom: true);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-setscale-arg-user")),
            2 => CompletionResult.FromHint(Loc.GetString("cmd-setscale-arg-scale")),
            _ => CompletionResult.Empty
        };
    }
}
