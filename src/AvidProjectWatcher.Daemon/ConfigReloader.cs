using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Configuration;
using AvidProjectWatcher.Core.State;
using AvidProjectWatcher.Core.Watching;

namespace AvidProjectWatcher.Daemon;

public sealed class ConfigReloader(
    IConfigStore configStore,
    ProjectObservationTracker observationTracker,
    WatchCoordinator watchCoordinator,
    IAuditLog auditLog,
    DaemonRuntimeState runtimeState)
{
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var config = await configStore.LoadAsync(cancellationToken);
        var state = await observationTracker.EnsureScopesAsync(config, cancellationToken);

        runtimeState.SetInstanceId(state.InstanceId);
        runtimeState.SetConfig(config);
        await watchCoordinator.ApplyConfigAsync(config, cancellationToken);

        await auditLog.AppendAsync(new AuditLogEntry
        {
            EventType = AuditEventType.ConfigReloaded,
            Trigger = "config",
            Message = $"Loaded {config.WatchedLocations.Count} watched scope(s)."
        }, cancellationToken);
    }
}
