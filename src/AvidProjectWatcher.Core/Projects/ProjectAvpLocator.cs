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

using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.Projects;

internal static class ProjectAvpLocator
{
    public static bool TryFindFirstAvpFileInDirectory(string directory, out string? avpPath)
    {
        try
        {
            avpPath = Directory
                .EnumerateFiles(directory, "*.avp", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, PathUtility.PathComparer)
                .FirstOrDefault();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            avpPath = null;
            return false;
        }
    }

    public static string? FindFirstProjectAvpOnPath(string rootPath, string avpPath)
    {
        var normalizedAvpPath = PathUtility.NormalizeFullPath(avpPath);
        var projectDirectory = Path.GetDirectoryName(normalizedAvpPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        var normalizedRootPath = PathUtility.NormalizeFullPath(rootPath);
        projectDirectory = PathUtility.NormalizeFullPath(projectDirectory);
        if (!PathUtility.IsPathUnder(projectDirectory, normalizedRootPath))
        {
            return null;
        }

        var pathSegments = new Stack<string>();
        var currentDirectory = projectDirectory;
        while (PathUtility.IsPathUnder(currentDirectory, normalizedRootPath))
        {
            pathSegments.Push(currentDirectory);
            if (PathUtility.AreSamePath(currentDirectory, normalizedRootPath))
            {
                break;
            }

            var parentDirectory = Path.GetDirectoryName(currentDirectory);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                break;
            }

            currentDirectory = PathUtility.NormalizeFullPath(parentDirectory);
        }

        while (pathSegments.Count > 0)
        {
            var directory = pathSegments.Pop();
            if (TryFindFirstAvpFileInDirectory(directory, out var firstAvpPath)
                && firstAvpPath is not null)
            {
                return firstAvpPath;
            }
        }

        return null;
    }
}
