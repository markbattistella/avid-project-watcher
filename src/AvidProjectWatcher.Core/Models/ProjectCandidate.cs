namespace AvidProjectWatcher.Core.Models;

public sealed record ProjectCandidate(
    string AvpPath,
    string ProjectDirectory,
    string RelativePath,
    Guid WatchedLocationId,
    bool IsExcluded,
    string? ExclusionReason);
