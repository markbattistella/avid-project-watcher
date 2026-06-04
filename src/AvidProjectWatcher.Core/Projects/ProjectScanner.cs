using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.Projects;

public sealed class ProjectScanner(ProjectResolver projectResolver)
{
    public async IAsyncEnumerable<ProjectCandidate> ScanAsync(
        WatcherConfig config,
        IEnumerable<WatchedLocation> scopes,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var selectedScopes = scopes.Where(scope => scope.Enabled).ToArray();
        var selectedScopeIds = selectedScopes.Select(scope => scope.Id).ToHashSet();
        var emittedProjectDirectories = new HashSet<string>(PathUtility.PathComparer);

        foreach (var scope in selectedScopes)
        {
            if (!Directory.Exists(scope.RootPath))
            {
                continue;
            }

            foreach (var avpPath in EnumerateAvpFilesSafely(scope.RootPath, cancellationToken))
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();

                var candidate = projectResolver.Resolve(config, avpPath);
                if (candidate is null)
                {
                    continue;
                }

                if (!selectedScopeIds.Contains(candidate.WatchedLocationId))
                {
                    continue;
                }

                if (emittedProjectDirectories.Add(candidate.ProjectDirectory))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateAvpFilesSafely(string rootPath, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(PathUtility.NormalizeFullPath(rootPath));
        var visitedDirectories = new HashSet<string>(PathUtility.PathComparer);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = pending.Pop();
            if (!visitedDirectories.Add(directory))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.avp", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var child in children)
            {
                try
                {
                    var attributes = File.GetAttributes(child);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                pending.Push(child);
            }
        }
    }
}
