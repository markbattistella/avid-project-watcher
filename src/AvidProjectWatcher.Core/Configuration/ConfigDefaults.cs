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

namespace AvidProjectWatcher.Core.Configuration;

public static class ConfigDefaults
{
    public static string DefaultApiListenHost =>
        Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_API_HOST")
        ?? "localhost";

    public static int DefaultApiPort =>
        int.TryParse(Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_API_PORT"), out var port)
            ? port
            : 47821;

    public static string DefaultApiListenUrl => $"http://{DefaultApiListenHost}:{DefaultApiPort}";

    public static string AppDataDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "AvidProjectWatcher");
        }
    }

    public static string DefaultConfigPath =>
        Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_CONFIG")
        ?? Path.Combine(AppDataDirectory, "config.json");

    public static string DefaultStatePath =>
        Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_STATE")
        ?? Path.Combine(AppDataDirectory, "state.json");

    public static string DefaultAuditDatabasePath =>
        Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_AUDIT_DB")
        ?? Path.Combine(AppDataDirectory, "audit.sqlite");
}
