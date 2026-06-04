namespace AvidProjectWatcher.Core.State;

public sealed record WatcherState
{
    public Guid InstanceId { get; init; } = Guid.NewGuid();

    public IReadOnlyList<ScopeRuntimeState> Scopes { get; init; } = [];
}

public sealed record ScopeRuntimeState
{
    public Guid ScopeId { get; init; }

    public DateTimeOffset ActivatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string> ObservedProjectDirectories { get; init; } = [];
}
