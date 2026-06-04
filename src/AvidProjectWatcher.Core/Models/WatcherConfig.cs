namespace AvidProjectWatcher.Core.Models;

public sealed record WatcherConfig
{
    public int Version { get; init; } = 1;

    public IReadOnlyList<WatchedLocation> WatchedLocations { get; init; } = [];

    public static WatcherConfig Empty { get; } = new();
}
