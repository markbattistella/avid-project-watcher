using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Core.Backfill;

public sealed record BackfillReport
{
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<FolderActionPlan> Plans { get; init; } = [];

    [JsonIgnore]
    public int AffectedProjectCount => Plans.Count(plan => plan.HasWork);
}
