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
