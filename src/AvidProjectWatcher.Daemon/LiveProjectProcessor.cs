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
using AvidProjectWatcher.Core.Folders;
using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Projects;
using AvidProjectWatcher.Core.State;

namespace AvidProjectWatcher.Daemon;

public sealed class LiveProjectProcessor(
    IConfigStore configStore,
    ProjectResolver projectResolver,
    FolderActionPlanner planner,
    FolderCreator folderCreator,
    ProjectObservationTracker observationTracker,
    IAuditLog auditLog)
{
    private readonly SemaphoreSlim processingGate = new(4, 4);

    public async Task HandleAvpAsync(
        string avpPath,
        FolderActionSource source,
        CancellationToken cancellationToken = default)
    {
        await processingGate.WaitAsync(cancellationToken);
        try
        {
            await HandleAvpCoreAsync(avpPath, source, cancellationToken);
        }
        finally
        {
            processingGate.Release();
        }
    }

    private async Task HandleAvpCoreAsync(
        string avpPath,
        FolderActionSource source,
        CancellationToken cancellationToken)
    {
        var config = await configStore.LoadAsync(cancellationToken);
        var candidate = projectResolver.Resolve(config, avpPath);
        if (candidate is null)
        {
            return;
        }

        var scope = config.WatchedLocations.Single(location => location.Id == candidate.WatchedLocationId);
        await auditLog.AppendAsync(new AuditLogEntry
        {
            EventType = AuditEventType.ProjectDetected,
            ScopeId = scope.Id,
            ScopeName = scope.Name,
            ProjectPath = candidate.ProjectDirectory,
            Trigger = source.ToString(),
            Message = $"Detected Avid project file '{candidate.AvpPath}'."
        }, cancellationToken);

        if (candidate.IsExcluded)
        {
            await observationTracker.MarkObservedAsync(scope.Id, candidate.ProjectDirectory, cancellationToken);
            await auditLog.AppendAsync(new AuditLogEntry
            {
                EventType = AuditEventType.ProjectSkipped,
                ScopeId = scope.Id,
                ScopeName = scope.Name,
                ProjectPath = candidate.ProjectDirectory,
                Trigger = source.ToString(),
                Message = candidate.ExclusionReason
            }, cancellationToken);
            return;
        }

        var plan = planner.CreatePlan(scope, candidate.ProjectDirectory, source);
        var result = await folderCreator.ApplyAsync(plan, cancellationToken);
        if (result.Succeeded)
        {
            await observationTracker.MarkObservedAsync(scope.Id, candidate.ProjectDirectory, cancellationToken);
        }

        await auditLog.AppendAsync(AuditEntryFactory.FromFolderResult(result), cancellationToken);
    }
}
