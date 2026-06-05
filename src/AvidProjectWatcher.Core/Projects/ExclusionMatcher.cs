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
