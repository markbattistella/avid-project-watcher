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
using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.Watching;

public sealed class ScopeFileWatcher : IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StabilityRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan StabilityTimeout = TimeSpan.FromSeconds(10);

    private readonly WatchedLocation scope;
    private readonly Func<string, FolderActionSource, CancellationToken, Task> onAvpDetected;
    private readonly Dictionary<string, CancellationTokenSource> pendingDetections = new(PathUtility.PathComparer);
    private readonly object gate = new();
    private FileSystemWatcher? watcher;
    private ScopeWatcherStatus status;

    public ScopeFileWatcher(
        WatchedLocation scope,
        Func<string, FolderActionSource, CancellationToken, Task> onAvpDetected)
    {
        this.scope = scope;
        this.onAvpDetected = onAvpDetected;
        status = new ScopeWatcherStatus
        {
            ScopeId = scope.Id,
            ScopeName = scope.Name,
            RootPath = scope.RootPath,
            IsRunning = false
        };
    }

    public ScopeWatcherStatus Status
    {
        get
        {
            lock (gate)
            {
                return status;
            }
        }
    }

    public void Start()
    {
        lock (gate)
        {
            if (!Directory.Exists(scope.RootPath))
            {
                status = status with
                {
                    IsRunning = false,
                    IsDisconnected = true,
                    Message = "Root path is not available."
                };
                return;
            }

            try
            {
                watcher = new FileSystemWatcher(scope.RootPath)
                {
                    IncludeSubdirectories = true,
                    Filter = "*.avp",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
                };

                watcher.Created += HandleCreated;
                watcher.Renamed += HandleRenamed;
                watcher.Error += HandleError;
                watcher.EnableRaisingEvents = true;

                status = status with
                {
                    IsRunning = true,
                    IsDisconnected = false,
                    Message = "Watching."
                };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                watcher?.Dispose();
                watcher = null;
                status = status with
                {
                    IsRunning = false,
                    IsDisconnected = true,
                    Message = exception.Message
                };
            }
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            watcher?.Dispose();
            watcher = null;

            foreach (var pending in pendingDetections.Values)
            {
                pending.Cancel();
                pending.Dispose();
            }

            pendingDetections.Clear();
            status = status with { IsRunning = false, Message = "Stopped." };
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void HandleCreated(object sender, FileSystemEventArgs args)
    {
        QueueDetection(args.FullPath);
    }

    private void HandleRenamed(object sender, RenamedEventArgs args)
    {
        QueueDetection(args.FullPath);
    }

    private void HandleError(object sender, ErrorEventArgs args)
    {
        lock (gate)
        {
            status = status with
            {
                IsRunning = false,
                IsDisconnected = true,
                Message = args.GetException().Message
            };
        }
    }

    private void QueueDetection(string path)
    {
        if (!PathUtility.IsAvpFile(path))
        {
            return;
        }

        var normalizedPath = PathUtility.NormalizeFullPath(path);
        CancellationTokenSource tokenSource;

        lock (gate)
        {
            if (pendingDetections.Remove(normalizedPath, out var existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            tokenSource = new CancellationTokenSource();
            pendingDetections[normalizedPath] = tokenSource;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelay, tokenSource.Token);
                await WaitForFileToSettleAsync(normalizedPath, tokenSource.Token);
                await onAvpDetected(normalizedPath, FolderActionSource.Live, tokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                lock (gate)
                {
                    status = status with
                    {
                        Message = exception.Message
                    };
                }
            }
            finally
            {
                lock (gate)
                {
                    if (pendingDetections.Remove(normalizedPath, out var current))
                    {
                        current.Dispose();
                    }
                }
            }
        });
    }

    private static async Task WaitForFileToSettleAsync(string filePath, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        long? previousLength = null;
        DateTime? previousWriteTime = null;

        while (DateTimeOffset.UtcNow - startedAt < StabilityTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                await Task.Delay(StabilityRetryDelay, cancellationToken);
                continue;
            }

            var info = new FileInfo(filePath);
            if (previousLength == info.Length && previousWriteTime == info.LastWriteTimeUtc)
            {
                return;
            }

            previousLength = info.Length;
            previousWriteTime = info.LastWriteTimeUtc;
            await Task.Delay(StabilityRetryDelay, cancellationToken);
        }
    }
}
