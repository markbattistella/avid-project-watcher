namespace AvidProjectWatcher.Core.Watching;

public sealed record ScopeWatcherStatus
{
    public Guid ScopeId { get; init; }

    public string ScopeName { get; init; } = string.Empty;

    public string RootPath { get; init; } = string.Empty;

    public bool IsRunning { get; init; }

    public bool IsDisconnected { get; init; }

    public string? Message { get; init; }
}
