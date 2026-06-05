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
