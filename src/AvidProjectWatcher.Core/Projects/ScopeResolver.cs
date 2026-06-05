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
