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

using System.Text.Json;
using AvidProjectWatcher.Core.Configuration;

namespace AvidProjectWatcher.Admin;

public sealed record Preferences
{
    public string DaemonBaseUrl { get; init; } = $"http://localhost:{ConfigDefaults.DefaultApiPort}";
}

public sealed class PreferencesStore
{
    private static readonly string PrefsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AvidProjectWatcher",
        "preferences.json");

    public Preferences Load()
    {
        try
        {
            if (!File.Exists(PrefsPath)) return new Preferences();
            var json = File.ReadAllText(PrefsPath);
            return JsonSerializer.Deserialize<Preferences>(json) ?? new Preferences();
        }
        catch
        {
            return new Preferences();
        }
    }

    public void Save(Preferences prefs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefsPath)!);
        File.WriteAllText(PrefsPath, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
    }
}
