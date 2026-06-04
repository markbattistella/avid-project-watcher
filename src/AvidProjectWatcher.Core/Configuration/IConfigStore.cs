using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Core.Configuration;

public interface IConfigStore
{
    string ConfigPath { get; }

    Task<WatcherConfig> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(WatcherConfig config, CancellationToken cancellationToken = default);
}
