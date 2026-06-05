// Avid Project Watcher
// Copyright (C) 2026  MB+MAB
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
