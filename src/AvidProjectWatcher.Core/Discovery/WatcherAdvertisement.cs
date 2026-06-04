namespace AvidProjectWatcher.Core.Discovery;

public sealed record WatcherAdvertisement
{
    public Guid InstanceId { get; init; }

    public string MachineName { get; init; } = Environment.MachineName;

    public DateTimeOffset SeenAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<AdvertisedScope> Scopes { get; init; } = [];
}

public sealed record AdvertisedScope
{
    public Guid ScopeId { get; init; }

    public string ScopeName { get; init; } = string.Empty;

    public string RootPath { get; init; } = string.Empty;
}
