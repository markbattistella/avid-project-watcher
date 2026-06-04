using AvidProjectWatcher.Core.Folders;
using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Projects;

namespace AvidProjectWatcher.Core.Backfill;

public sealed class BackfillEngine(ProjectScanner scanner, FolderActionPlanner planner, FolderCreator folderCreator)
{
    public async Task<BackfillReport> DryRunAsync(
        WatcherConfig config,
        BackfillRequest request,
        CancellationToken cancellationToken = default)
    {
        var scopes = SelectScopes(config, request).ToArray();
        var plans = new List<FolderActionPlan>();

        await foreach (var candidate in scanner.ScanAsync(config, scopes, cancellationToken))
        {
            var scope = scopes.Single(scope => scope.Id == candidate.WatchedLocationId);
            var plan = candidate.IsExcluded
                ? planner.CreatePlan(scope, candidate.ProjectDirectory, FolderActionSource.ManualBackfill, candidate.ExclusionReason)
                : planner.CreatePlan(scope, candidate.ProjectDirectory, FolderActionSource.ManualBackfill);

            if (plan.HasWork)
            {
                plans.Add(plan);
            }
        }

        return new BackfillReport { Plans = plans };
    }

    public async Task<IReadOnlyList<FolderActionResult>> CommitAsync(
        BackfillReport report,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FolderActionResult>();

        foreach (var plan in report.Plans.Where(plan => plan.HasWork))
        {
            results.Add(await folderCreator.ApplyAsync(plan, cancellationToken));
        }

        return results;
    }

    private static IEnumerable<WatchedLocation> SelectScopes(WatcherConfig config, BackfillRequest request)
    {
        var selectedIds = request.ScopeIds.ToHashSet();
        return selectedIds.Count == 0
            ? config.WatchedLocations.Where(scope => scope.Enabled)
            : config.WatchedLocations.Where(scope => scope.Enabled && selectedIds.Contains(scope.Id));
    }
}
