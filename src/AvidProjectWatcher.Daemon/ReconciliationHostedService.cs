// Avid Project Watcher
// Copyright (C) 2026  MB+MAB
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

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
