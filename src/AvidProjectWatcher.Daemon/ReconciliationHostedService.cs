using AvidProjectWatcher.Core.Configuration;
using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Projects;
using AvidProjectWatcher.Core.State;

namespace AvidProjectWatcher.Daemon;

public sealed class ReconciliationHostedService(
    IConfigStore configStore,
    ProjectScanner scanner,
    ProjectObservationTracker observationTracker,
    LiveProjectProcessor processor,
    ILogger<ReconciliationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reconciliation failed.");
            }

            await Task.Delay(ReconciliationInterval, stoppingToken);
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var config = await configStore.LoadAsync(cancellationToken);
        var scopes = config.WatchedLocations.Where(scope => scope.Enabled).ToArray();

        await foreach (var candidate in scanner.ScanAsync(config, scopes, cancellationToken))
        {
            if (await observationTracker.HasObservedAsync(candidate.WatchedLocationId, candidate.ProjectDirectory, cancellationToken))
            {
                continue;
            }

            var activationTime = await observationTracker.GetActivationTimeAsync(candidate.WatchedLocationId, cancellationToken);
            var avpCreatedAt = File.GetCreationTimeUtc(candidate.AvpPath);
            if (avpCreatedAt < activationTime.UtcDateTime)
            {
                continue;
            }

            await processor.HandleAvpAsync(candidate.AvpPath, FolderActionSource.NewReconciliation, cancellationToken);
        }
    }
}
