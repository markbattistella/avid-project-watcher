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
using AvidProjectWatcher.Core.Watching;

namespace AvidProjectWatcher.Daemon;

public sealed class WatcherRecoveryHostedService(
    DaemonRuntimeState runtimeState,
    WatchCoordinator watchCoordinator,
    IAuditLog auditLog,
    ILogger<WatcherRecoveryHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(5);
    private readonly HashSet<Guid> knownDisconnected = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Watcher recovery pass failed.");
            }

            await Task.Delay(RecoveryInterval, stoppingToken);
        }
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var statuses = watchCoordinator.Statuses;
        var nowDisconnected = statuses
            .Where(s => s.IsDisconnected)
            .Select(s => s.ScopeId)
            .ToHashSet();

        foreach (var status in statuses.Where(s => s.IsDisconnected && knownDisconnected.Add(s.ScopeId)))
        {
            await auditLog.AppendAsync(new AuditLogEntry
            {
                EventType = AuditEventType.WatcherError,
                ScopeId = status.ScopeId,
                ScopeName = status.ScopeName,
                Trigger = "recovery",
                Message = status.Message,
                IsError = true
            }, cancellationToken);
        }

        knownDisconnected.IntersectWith(nowDisconnected);

        await watchCoordinator.RestartDisconnectedAsync(runtimeState.CurrentConfig, cancellationToken);
    }
}
