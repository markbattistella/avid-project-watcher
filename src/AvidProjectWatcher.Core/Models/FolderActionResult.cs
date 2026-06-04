namespace AvidProjectWatcher.Core.Models;

public sealed record FolderActionResult
{
    public Guid WatchedLocationId { get; init; }

    public string ScopeName { get; init; } = string.Empty;

    public string ProjectDirectory { get; init; } = string.Empty;

    public IReadOnlyList<string> FoldersCreated { get; init; } = [];

    public IReadOnlyList<string> FoldersAlreadyPresent { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public FolderActionSource Source { get; init; }
}
