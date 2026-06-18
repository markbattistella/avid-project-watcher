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

using AvidProjectWatcher.Core.Discovery;
using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Daemon;

public sealed class DaemonRuntimeState
{
    private static readonly TimeSpan RemoteAdvertisementTtl = TimeSpan.FromSeconds(45);
    private readonly object gate = new();
    private readonly Dictionary<Guid, WatcherAdvertisement> remoteAdvertisements = [];
    private WatcherConfig currentConfig = WatcherConfig.Empty;
    private DateTimeOffset? lastConfigReloadUtc;
    private bool isShuttingDown;
    private bool restartRequested;

    public Guid InstanceId { get; private set; } = Guid.NewGuid();

    public WatcherConfig CurrentConfig
    {
        get
        {
            lock (gate)
            {
                return currentConfig;
            }
        }
    }

    public DateTimeOffset? LastConfigReloadUtc
    {
        get
        {
            lock (gate)
            {
                return lastConfigReloadUtc;
            }
        }
    }

    public bool IsShuttingDown
    {
        get
        {
            lock (gate)
            {
                return isShuttingDown;
            }
        }
    }

    public bool RestartRequested
    {
        get
        {
            lock (gate)
            {
                return restartRequested;
            }
        }
    }

    public void SetInstanceId(Guid instanceId)
    {
        lock (gate)
        {
            InstanceId = instanceId;
        }
    }

    public void SetConfig(WatcherConfig config)
    {
        lock (gate)
        {
            currentConfig = config;
            lastConfigReloadUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RequestStop()
    {
        lock (gate)
        {
            isShuttingDown = true;
        }
    }

    public void RequestRestart()
    {
        lock (gate)
        {
            isShuttingDown = true;
            restartRequested = true;
        }
    }

    public void RecordRemoteAdvertisement(WatcherAdvertisement advertisement)
    {
        lock (gate)
        {
            remoteAdvertisements[advertisement.InstanceId] = advertisement with { SeenAtUtc = DateTimeOffset.UtcNow };
        }
    }

    public IReadOnlyList<WatcherAdvertisement> GetRemoteAdvertisements()
    {
        lock (gate)
        {
            var cutoff = DateTimeOffset.UtcNow - RemoteAdvertisementTtl;
            var expired = remoteAdvertisements
                .Where(pair => pair.Value.SeenAtUtc < cutoff)
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var id in expired)
            {
                remoteAdvertisements.Remove(id);
            }

            return remoteAdvertisements.Values.ToArray();
        }
    }
}
