using AvidProjectWatcher.Core.Discovery;
using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Daemon;

public sealed class DaemonRuntimeState
{
    private static readonly TimeSpan RemoteAdvertisementTtl = TimeSpan.FromSeconds(45);
    private readonly object gate = new();
    private readonly Dictionary<Guid, WatcherAdvertisement> remoteAdvertisements = [];
    private WatcherConfig currentConfig = WatcherConfig.Empty;

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

    public DateTimeOffset? LastConfigReloadUtc { get; private set; }

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
            LastConfigReloadUtc = DateTimeOffset.UtcNow;
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
