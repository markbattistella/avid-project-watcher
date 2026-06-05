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

namespace AvidProjectWatcher.Core.Tests;

internal sealed class TemporaryWorkspace : IDisposable
{
    private TemporaryWorkspace(string rootPath)
    {
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public static TemporaryWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "AvidProjectWatcher.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TemporaryWorkspace(root);
    }

    public string GetPath(params string[] segments)
    {
        return segments.Length == 0 ? RootPath : Path.Combine([RootPath, .. segments]);
    }

    public string CreateDirectory(params string[] segments)
    {
        var path = GetPath(segments);
        Directory.CreateDirectory(path);
        return path;
    }

    public string CreateFile(string[] segments)
    {
        var path = GetPath(segments);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, string.Empty);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
