using AvidProjectWatcher.Core.Discovery;
using AvidProjectWatcher.Core.Watching;

namespace AvidProjectWatcher.Daemon;

public sealed record DaemonStatus
{
    public Guid InstanceId { get; init; }

    public string MachineName { get; init; } = string.Empty;

    public string ConfigPath { get; init; } = string.Empty;

    public string StatePath { get; init; } = string.Empty;

    public string AuditDatabasePath { get; init; } = string.Empty;

    public DateTimeOffset? LastConfigReloadUtc { get; init; }

    public IReadOnlyList<ScopeWatcherStatus> Watchers { get; init; } = [];

    public IReadOnlyList<DuplicateWatcherWarning> DuplicateWarnings { get; init; } = [];
}
