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
    public async Task HandleAvpAsync(
        string avpPath,
        FolderActionSource source,
        CancellationToken cancellationToken = default)
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
        await observationTracker.MarkObservedAsync(scope.Id, candidate.ProjectDirectory, cancellationToken);
        await auditLog.AppendAsync(AuditEntryFactory.FromFolderResult(result), cancellationToken);
    }
}
