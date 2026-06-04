using AvidProjectWatcher.Core.Watching;

namespace AvidProjectWatcher.Daemon;

public sealed class WatcherRecoveryHostedService(
    DaemonRuntimeState runtimeState,
    WatchCoordinator watchCoordinator,
    ILogger<WatcherRecoveryHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await watchCoordinator.RestartDisconnectedAsync(runtimeState.CurrentConfig, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Watcher recovery pass failed.");
            }

            await Task.Delay(RecoveryInterval, stoppingToken);
        }
    }
}
