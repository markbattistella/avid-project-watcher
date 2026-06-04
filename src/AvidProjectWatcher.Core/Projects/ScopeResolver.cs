using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.Projects;

public sealed class ScopeResolver
{
    public WatchedLocation? ResolveOwner(WatcherConfig config, string candidatePath)
    {
        return config.WatchedLocations
            .Where(scope => scope.Enabled)
            .Where(scope => PathUtility.IsPathUnder(candidatePath, scope.RootPath))
            .OrderByDescending(scope => PathUtility.NormalizeFullPath(scope.RootPath).Length)
            .FirstOrDefault();
    }

    public IReadOnlyList<ScopeOverlap> FindOverlaps(WatcherConfig config)
    {
        var scopes = config.WatchedLocations
            .Where(scope => scope.Enabled && !string.IsNullOrWhiteSpace(scope.RootPath))
            .ToArray();

        var overlaps = new List<ScopeOverlap>();

        for (var index = 0; index < scopes.Length; index++)
        {
            for (var otherIndex = index + 1; otherIndex < scopes.Length; otherIndex++)
            {
                var first = scopes[index];
                var second = scopes[otherIndex];

                if (PathUtility.IsPathUnder(first.RootPath, second.RootPath)
                    || PathUtility.IsPathUnder(second.RootPath, first.RootPath))
                {
                    overlaps.Add(new ScopeOverlap(first, second));
                }
            }
        }

        return overlaps;
    }
}
