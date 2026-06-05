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
using AvidProjectWatcher.Core.State;
using AvidProjectWatcher.Core.Watching;

namespace AvidProjectWatcher.Daemon;

public sealed class ConfigReloader(
    IConfigStore configStore,
    ProjectObservationTracker observationTracker,
    WatchCoordinator watchCoordinator,
    IAuditLog auditLog,
    DaemonRuntimeState runtimeState)
{
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var config = await configStore.LoadAsync(cancellationToken);
        var state = await observationTracker.EnsureScopesAsync(config, cancellationToken);

        runtimeState.SetInstanceId(state.InstanceId);
        runtimeState.SetConfig(config);
        await watchCoordinator.ApplyConfigAsync(config, cancellationToken);

        await auditLog.AppendAsync(new AuditLogEntry
        {
            EventType = AuditEventType.ConfigReloaded,
            Trigger = "config",
            Message = $"Loaded {config.WatchedLocations.Count} watched scope(s)."
        }, cancellationToken);
    }
}
