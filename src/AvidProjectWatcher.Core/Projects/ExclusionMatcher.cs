using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.Projects;

public sealed class ExclusionMatcher
{
    public ExclusionMatch Match(WatchedLocation scope, string projectDirectory)
    {
        foreach (var exclusion in scope.ExcludedPaths)
        {
            if (string.IsNullOrWhiteSpace(exclusion.Path))
            {
                continue;
            }

            var exclusionPath = Path.IsPathRooted(exclusion.Path)
                ? exclusion.Path
                : Path.Combine(scope.RootPath, exclusion.Path);

            if (PathUtility.IsPathUnder(projectDirectory, exclusionPath))
            {
                return new ExclusionMatch(true, $"Project is inside excluded path '{exclusion.Path}'.");
            }
        }

        return ExclusionMatch.NotExcluded;
    }
}
