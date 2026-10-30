using System.Threading;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;

namespace Content.Server.Administration.Logs;

public sealed class LogFilter
{
    public CancellationToken CancellationToken { get; set; }

    public int? Round { get; set; }

    public string? Search { get; set; }

    public HashSet<LogType>? Types { get; set; }

    public HashSet<LogImpact>? Impacts { get; set; }

    public DateTime? Before { get; set; }

    public DateTime? After { get; set; }

    public bool IncludePlayers  { get; set; } = true;

    public Guid[]? AnyPlayers { get; set; }

    public Guid[]? AllPlayers { get; set; }

    public bool IncludeNonPlayers { get; set; }

    public int? LastLogId { get; set; }

    public int LogsSent { get; set; }

    public int? Limit { get; set; }

    public DateOrder DateOrder { get; set; } = DateOrder.Descending;

    // Claw Command start - antag reveal gate
    /// <summary>
    /// Logs of these types are excluded unless every player tagged on them is in <see cref="VisiblePlayers"/>.
    /// </summary>
    public HashSet<LogType>? HiddenTypes { get; set; }

    public Guid[] VisiblePlayers { get; set; } = [];
    // Claw Command end
}
