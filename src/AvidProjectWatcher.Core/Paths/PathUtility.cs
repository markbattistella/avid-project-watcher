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

namespace AvidProjectWatcher.Core.Paths;

public static class PathUtility
{
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public static string NormalizeFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        var normalized = Path.GetFullPath(expanded);
        return TrimTrailingSeparators(normalized);
    }

    public static bool IsAvpFile(string path)
    {
        return string.Equals(Path.GetExtension(path), ".avp", StringComparison.OrdinalIgnoreCase);
    }

    public static bool AreSamePath(string left, string right)
    {
        return PathComparer.Equals(NormalizeFullPath(left), NormalizeFullPath(right));
    }

    public static bool IsPathUnder(string candidatePath, string parentPath, bool includeParent = true)
    {
        var candidate = NormalizeFullPath(candidatePath);
        var parent = NormalizeFullPath(parentPath);

        if (candidate.Length == 0 || parent.Length == 0)
        {
            return false;
        }

        if (includeParent && PathComparer.Equals(candidate, parent))
        {
            return true;
        }

        if (!candidate.StartsWith(parent, ToComparison(PathComparer)))
        {
            return false;
        }

        return candidate.Length > parent.Length
            && IsSeparator(candidate[parent.Length]);
    }

    public static string GetRelativePath(string rootPath, string candidatePath)
    {
        return Path.GetRelativePath(NormalizeFullPath(rootPath), NormalizeFullPath(candidatePath));
    }

    private static string TrimTrailingSeparators(string path)
    {
        if (Path.GetPathRoot(path) == path)
        {
            return path;
        }

        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSeparator(char value)
    {
        return value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;
    }

    private static StringComparison ToComparison(StringComparer comparer)
    {
        return ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
