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

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Discovery;
using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Daemon;

public sealed class LanDiscoveryHostedService(
    DaemonRuntimeState runtimeState,
    DuplicateWatcherDetector duplicateDetector,
    IAuditLog auditLog,
    ILogger<LanDiscoveryHostedService> logger) : BackgroundService
{
    private const int DiscoveryPort = 47822;
    private const string DiscoveryProbe = "avid-project-watcher-discovery-v1";
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var receiver = new UdpClient(AddressFamily.InterNetwork);
        using var sender = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };

        try
        {
            receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            logger.LogWarning(exception, "LAN discovery disabled because the UDP listener could not start.");
            return;
        }

        var receiveTask = ReceiveLoopAsync(receiver, sender, stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await BroadcastAsync(sender, stoppingToken);
                await Task.Delay(BroadcastInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            receiver.Dispose();
            sender.Dispose();

            try
            {
                await receiveTask;
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private async Task BroadcastAsync(UdpClient sender, CancellationToken cancellationToken)
    {
        var config = runtimeState.CurrentConfig;
        var advertisement = new WatcherAdvertisement
        {
            InstanceId = runtimeState.InstanceId,
            MachineName = Environment.MachineName,
            Scopes = config.WatchedLocations
                .Where(scope => scope.Enabled)
                .Select(ToAdvertisedScope)
                .ToArray()
        };

        var json = JsonSerializer.Serialize(advertisement, JsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);
        await sender.SendAsync(payload, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort), cancellationToken);
    }

    private async Task ReceiveLoopAsync(UdpClient receiver, UdpClient sender, CancellationToken cancellationToken)
    {
        var warnedPairs = new HashSet<(Guid, Guid)>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await receiver.ReceiveAsync(cancellationToken);
                var json = Encoding.UTF8.GetString(result.Buffer);
                if (string.Equals(json.Trim(), DiscoveryProbe, StringComparison.Ordinal))
                {
                    await BroadcastAsync(sender, cancellationToken);
                    continue;
                }

                var advertisement = JsonSerializer.Deserialize<WatcherAdvertisement>(json, JsonOptions);
                if (advertisement is null || advertisement.InstanceId == runtimeState.InstanceId)
                {
                    continue;
                }

                runtimeState.RecordRemoteAdvertisement(advertisement);

                var warnings = duplicateDetector.FindWarnings(
                    runtimeState.InstanceId,
                    runtimeState.CurrentConfig.WatchedLocations,
                    [advertisement]);

                foreach (var warning in warnings)
                {
                    var pair = (runtimeState.InstanceId, advertisement.InstanceId);
                    if (!warnedPairs.Add(pair))
                    {
                        continue;
                    }

                    var message = $"Duplicate watcher detected on {warning.RemoteMachineName}: " +
                        $"remote scope '{warning.RemoteScopeName}' ({warning.RemoteRootPath}) " +
                        $"overlaps with local scope '{warning.LocalScopeName}' ({warning.LocalRootPath}).";

                    logger.LogCritical("Anti-farm conflict: {Message}", message);

                    await auditLog.AppendAsync(new AuditLogEntry
                    {
                        EventType = AuditEventType.DuplicateWatcherWarning,
                        Trigger = "lan-discovery",
                        Message = message,
                        IsError = true
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception exception) when (exception is SocketException or JsonException)
            {
                logger.LogDebug(exception, "Ignored invalid LAN discovery packet.");
            }
        }
    }

    private static AdvertisedScope ToAdvertisedScope(WatchedLocation scope)
    {
        return new AdvertisedScope
        {
            ScopeId = scope.Id,
            ScopeName = scope.Name,
            RootPath = scope.RootPath
        };
    }
}
