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

namespace AvidProjectWatcher.Core.State;

public sealed class JsonStateStore(string statePath) : IStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<WatcherState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(statePath))
        {
            return new WatcherState();
        }

        await using var stream = File.OpenRead(statePath);
        return await JsonSerializer.DeserializeAsync<WatcherState>(stream, SerializerOptions, cancellationToken)
            ?? new WatcherState();
    }

    public async Task SaveAsync(WatcherState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(statePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }
}
