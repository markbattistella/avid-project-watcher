namespace AvidProjectWatcher.Core.Audit;

public enum AuditEventType
{
    ProjectDetected,
    ProjectSkipped,
    FolderPlanCreated,
    FoldersCreated,
    BackfillDryRun,
    BackfillCommitted,
    WatcherError,
    DuplicateWatcherWarning,
    ConfigReloaded
}
