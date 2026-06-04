namespace AvidProjectWatcher.Core.Models;

public sealed record WatchedLocation
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public string RootPath { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    public IReadOnlyList<FolderTemplateEntry> FolderTemplate { get; init; } = [];

    public IReadOnlyList<ExcludedPath> ExcludedPaths { get; init; } = [];
}
