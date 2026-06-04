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
