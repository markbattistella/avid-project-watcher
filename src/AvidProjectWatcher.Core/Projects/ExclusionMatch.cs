namespace AvidProjectWatcher.Core.Projects;

public sealed record ExclusionMatch(bool IsExcluded, string? Reason)
{
    public static ExclusionMatch NotExcluded { get; } = new(false, null);
}
