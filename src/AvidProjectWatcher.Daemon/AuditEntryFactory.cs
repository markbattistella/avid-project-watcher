using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Daemon;

public static class AuditEntryFactory
{
    public static AuditLogEntry FromFolderResult(FolderActionResult result)
    {
        return new AuditLogEntry
        {
            EventType = result.Errors.Count > 0 ? AuditEventType.WatcherError : AuditEventType.FoldersCreated,
            ScopeId = result.WatchedLocationId,
            ScopeName = result.ScopeName,
            ProjectPath = result.ProjectDirectory,
            Trigger = result.Source.ToString(),
            FoldersCreated = result.FoldersCreated,
            FoldersAlreadyPresent = result.FoldersAlreadyPresent,
            Message = result.Errors.Count > 0 ? string.Join(" ", result.Errors) : "Folder creation completed.",
            IsError = result.Errors.Count > 0
        };
    }
}
