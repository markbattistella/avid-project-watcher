namespace AvidProjectWatcher.Core.Backfill;

public sealed record BackfillRequest
{
    public IReadOnlyList<Guid> ScopeIds { get; init; } = [];
}
