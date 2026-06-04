using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Core.Watching;

public sealed class WatchCoordinator : IDisposable
{
    private readonly Func<string, FolderActionSource, CancellationToken, Task> onAvpDetected;
    private readonly List<ScopeFileWatcher> watchers = [];
    private readonly object gate = new();

    public WatchCoordinator(Func<string, FolderActionSource, CancellationToken, Task> onAvpDetected)
    {
        this.onAvpDetected = onAvpDetected;
    }

    public IReadOnlyList<ScopeWatcherStatus> Statuses
    {
        get
        {
            lock (gate)
            {
                return watchers.Select(watcher => watcher.Status).ToArray();
            }
        }
    }

    public Task ApplyConfigAsync(WatcherConfig config, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }

            watchers.Clear();

            foreach (var scope in config.WatchedLocations.Where(scope => scope.Enabled))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var watcher = new ScopeFileWatcher(scope, onAvpDetected);
                watcher.Start();
                watchers.Add(watcher);
            }
        }

        return Task.CompletedTask;
    }

    public Task RestartDisconnectedAsync(WatcherConfig config, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            for (var index = 0; index < watchers.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var existing = watchers[index];
                var status = existing.Status;
                if (!status.IsDisconnected)
                {
                    continue;
                }

                var scope = config.WatchedLocations.FirstOrDefault(scope => scope.Id == status.ScopeId && scope.Enabled);
                if (scope is null)
                {
                    continue;
                }

                existing.Dispose();
                var replacement = new ScopeFileWatcher(scope, onAvpDetected);
                replacement.Start();
                watchers[index] = replacement;
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }

            watchers.Clear();
        }
    }
}
