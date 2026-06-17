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

using Microsoft.Data.Sqlite;

namespace AvidProjectWatcher.Core.Audit;

public sealed class SqliteAuditLog(string databasePath) : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await InitializeCoreAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AppendAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await InitializeCoreAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO audit_log (
                    timestamp_utc,
                    event_type,
                    scope_id,
                    scope_name,
                    project_path,
                    trigger,
                    folders_created_json,
                    folders_already_present_json,
                    message,
                    is_error
                )
                VALUES (
                    $timestamp_utc,
                    $event_type,
                    $scope_id,
                    $scope_name,
                    $project_path,
                    $trigger,
                    $folders_created_json,
                    $folders_already_present_json,
                    $message,
                    $is_error
                );
                """;

            command.Parameters.AddWithValue("$timestamp_utc", entry.TimestampUtc.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("$event_type", entry.EventType.ToString());
            command.Parameters.AddWithNullableValue("$scope_id", entry.ScopeId?.ToString());
            command.Parameters.AddWithNullableValue("$scope_name", entry.ScopeName);
            command.Parameters.AddWithNullableValue("$project_path", entry.ProjectPath);
            command.Parameters.AddWithValue("$trigger", entry.Trigger);
            command.Parameters.AddWithValue("$folders_created_json", JsonSerializer.Serialize(entry.FoldersCreated, JsonOptions));
            command.Parameters.AddWithValue("$folders_already_present_json", JsonSerializer.Serialize(entry.FoldersAlreadyPresent, JsonOptions));
            command.Parameters.AddWithNullableValue("$message", entry.Message);
            command.Parameters.AddWithValue("$is_error", entry.IsError ? 1 : 0);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await InitializeCoreAsync(cancellationToken);

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            var whereClauses = new List<string>();

            if (query.ScopeId is not null)
            {
                whereClauses.Add("scope_id = $scope_id");
                command.Parameters.AddWithValue("$scope_id", query.ScopeId.ToString());
            }

            if (query.FromUtc is not null)
            {
                whereClauses.Add("timestamp_utc >= $from_utc");
                command.Parameters.AddWithValue("$from_utc", query.FromUtc.Value.UtcDateTime.ToString("O"));
            }

            if (query.ToUtc is not null)
            {
                whereClauses.Add("timestamp_utc <= $to_utc");
                command.Parameters.AddWithValue("$to_utc", query.ToUtc.Value.UtcDateTime.ToString("O"));
            }

            if (query.EventType is not null)
            {
                whereClauses.Add("event_type = $event_type");
                command.Parameters.AddWithValue("$event_type", query.EventType.Value.ToString());
            }

            if (query.IsError is not null)
            {
                whereClauses.Add("is_error = $is_error");
                command.Parameters.AddWithValue("$is_error", query.IsError.Value ? 1 : 0);
            }

            command.Parameters.AddWithValue("$limit", Math.Clamp(query.Limit, 1, 5_000));
            command.CommandText = $"""
                SELECT
                    id,
                    timestamp_utc,
                    event_type,
                    scope_id,
                    scope_name,
                    project_path,
                    trigger,
                    folders_created_json,
                    folders_already_present_json,
                    message,
                    is_error
                FROM audit_log
                {(whereClauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", whereClauses))}
                ORDER BY timestamp_utc DESC, id DESC
                LIMIT $limit;
                """;

            var entries = new List<AuditLogEntry>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(new AuditLogEntry
                {
                    Id = reader.GetInt64(0),
                    TimestampUtc = DateTimeOffset.Parse(reader.GetString(1)),
                    EventType = Enum.Parse<AuditEventType>(reader.GetString(2)),
                    ScopeId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                    ScopeName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ProjectPath = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Trigger = reader.GetString(6),
                    FoldersCreated = JsonSerializer.Deserialize<string[]>(reader.GetString(7), JsonOptions) ?? [],
                    FoldersAlreadyPresent = JsonSerializer.Deserialize<string[]>(reader.GetString(8), JsonOptions) ?? [],
                    Message = reader.IsDBNull(9) ? null : reader.GetString(9),
                    IsError = reader.GetInt32(10) == 1
                });
            }

            return entries;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        if (initialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS audit_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp_utc TEXT NOT NULL,
                event_type TEXT NOT NULL,
                scope_id TEXT NULL,
                scope_name TEXT NULL,
                project_path TEXT NULL,
                trigger TEXT NOT NULL,
                folders_created_json TEXT NOT NULL,
                folders_already_present_json TEXT NOT NULL,
                message TEXT NULL,
                is_error INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_audit_log_timestamp_utc ON audit_log(timestamp_utc);
            CREATE INDEX IF NOT EXISTS ix_audit_log_scope_id ON audit_log(scope_id);
            CREATE INDEX IF NOT EXISTS ix_audit_log_event_type ON audit_log(event_type);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        initialized = true;
    }

    private SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        return new SqliteConnection(builder.ToString());
    }
}

internal static class SqliteParameterExtensions
{
    public static void AddWithNullableValue(this SqliteParameterCollection parameters, string name, string? value)
    {
        parameters.AddWithValue(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value);
    }
}
