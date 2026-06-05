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

namespace AvidProjectWatcher.Core.Configuration;

public sealed class JsonConfigStore(string configPath) : IConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string ConfigPath { get; } = configPath;

    public async Task<WatcherConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            return WatcherConfig.Empty;
        }

        await using var stream = File.OpenRead(ConfigPath);
        return await JsonSerializer.DeserializeAsync<WatcherConfig>(stream, SerializerOptions, cancellationToken)
            ?? WatcherConfig.Empty;
    }

    public async Task SaveAsync(WatcherConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{ConfigPath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, SerializerOptions, cancellationToken);
        }

        if (File.Exists(ConfigPath))
        {
            File.Replace(tempPath, ConfigPath, null);
        }
        else
        {
            File.Move(tempPath, ConfigPath);
        }
    }
}
