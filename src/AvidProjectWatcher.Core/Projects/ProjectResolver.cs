using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.Projects;

public sealed class ProjectResolver(ScopeResolver scopeResolver, ExclusionMatcher exclusionMatcher)
{
    public ProjectCandidate? Resolve(WatcherConfig config, string avpPath)
    {
        if (!PathUtility.IsAvpFile(avpPath))
        {
            return null;
        }

        var normalizedAvpPath = PathUtility.NormalizeFullPath(avpPath);
        var projectDirectory = Path.GetDirectoryName(normalizedAvpPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        projectDirectory = PathUtility.NormalizeFullPath(projectDirectory);
        var owningScope = scopeResolver.ResolveOwner(config, projectDirectory);
        if (owningScope is null)
        {
            return null;
        }

        var exclusion = exclusionMatcher.Match(owningScope, projectDirectory);
        return new ProjectCandidate(
            normalizedAvpPath,
            projectDirectory,
            PathUtility.GetRelativePath(owningScope.RootPath, projectDirectory),
            owningScope.Id,
            exclusion.IsExcluded,
            exclusion.Reason);
    }
}
