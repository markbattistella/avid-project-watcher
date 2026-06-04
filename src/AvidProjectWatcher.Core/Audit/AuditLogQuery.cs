namespace AvidProjectWatcher.Core.Audit;

public sealed record AuditLogQuery
{
    public Guid? ScopeId { get; init; }

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public AuditEventType? EventType { get; init; }

    public bool? IsError { get; init; }

    public int Limit { get; init; } = 250;
}
