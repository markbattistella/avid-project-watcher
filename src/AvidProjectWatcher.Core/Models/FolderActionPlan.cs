namespace AvidProjectWatcher.Core.Models;

public enum FolderActionSource
{
    Live,
    NewReconciliation,
    ManualBackfill
}

public sealed record FolderActionPlan
{
    public Guid WatchedLocationId { get; init; }

    public string ScopeName { get; init; } = string.Empty;

    public string ProjectDirectory { get; init; } = string.Empty;

    public IReadOnlyList<string> FoldersToCreate { get; init; } = [];

    public IReadOnlyList<string> FoldersAlreadyPresent { get; init; } = [];

    public string? SkippedReason { get; init; }

    public FolderActionSource Source { get; init; }

    [JsonIgnore]
    public bool HasWork => FoldersToCreate.Count > 0 && string.IsNullOrWhiteSpace(SkippedReason);
}
