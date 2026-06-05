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

using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Configuration;

namespace AvidProjectWatcher.Daemon;

public sealed class ConfigWatcherHostedService(
    IConfigStore configStore,
    ConfigReloader reloader,
    IAuditLog auditLog,
    ILogger<ConfigWatcherHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await auditLog.InitializeAsync(stoppingToken);
        await reloader.ReloadAsync(stoppingToken);

        var configDirectory = Path.GetDirectoryName(configStore.ConfigPath);
        var configFileName = Path.GetFileName(configStore.ConfigPath);
        if (string.IsNullOrWhiteSpace(configDirectory) || string.IsNullOrWhiteSpace(configFileName))
        {
            return;
        }

        Directory.CreateDirectory(configDirectory);
        using var watcher = new FileSystemWatcher(configDirectory, configFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        watcher.Changed += (_, _) => QueueReload();
        watcher.Created += (_, _) => QueueReload();
        watcher.Renamed += (_, _) => QueueReload();
        watcher.EnableRaisingEvents = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        void QueueReload()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                    await reloader.ReloadAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to reload watcher config.");
                }
            }, stoppingToken);
        }
    }
}
