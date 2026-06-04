namespace AvidProjectWatcher.Core.Audit;

public interface IAuditLog
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
