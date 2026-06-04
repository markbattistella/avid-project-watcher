using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.Discovery;

public sealed class DuplicateWatcherDetector
{
    public IReadOnlyList<DuplicateWatcherWarning> FindWarnings(
        Guid localInstanceId,
        IEnumerable<WatchedLocation> localScopes,
        IEnumerable<WatcherAdvertisement> remoteAdvertisements)
    {
        var warnings = new List<DuplicateWatcherWarning>();
        var enabledLocalScopes = localScopes.Where(scope => scope.Enabled).ToArray();

        foreach (var advertisement in remoteAdvertisements.Where(ad => ad.InstanceId != localInstanceId))
        {
            foreach (var localScope in enabledLocalScopes)
            {
                foreach (var remoteScope in advertisement.Scopes)
                {
                    if (PathUtility.IsPathUnder(localScope.RootPath, remoteScope.RootPath)
                        || PathUtility.IsPathUnder(remoteScope.RootPath, localScope.RootPath))
                    {
                        warnings.Add(new DuplicateWatcherWarning(
                            advertisement.InstanceId,
                            advertisement.MachineName,
                            localScope.Name,
                            remoteScope.ScopeName,
                            localScope.RootPath,
                            remoteScope.RootPath));
                    }
                }
            }
        }

        return warnings;
    }
}
