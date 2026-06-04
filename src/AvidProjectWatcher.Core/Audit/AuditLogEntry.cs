namespace AvidProjectWatcher.Core.Audit;

public sealed record AuditLogEntry
{
    public long Id { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public AuditEventType EventType { get; init; }

    public Guid? ScopeId { get; init; }

    public string? ScopeName { get; init; }

    public string? ProjectPath { get; init; }

    public string Trigger { get; init; } = string.Empty;

    public IReadOnlyList<string> FoldersCreated { get; init; } = [];

    public IReadOnlyList<string> FoldersAlreadyPresent { get; init; } = [];

    public string? Message { get; init; }

    public bool IsError { get; init; }
}
