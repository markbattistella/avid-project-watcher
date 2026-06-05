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

using System.Net.Http.Json;
using System.Text;
using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Backfill;
using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Admin.ViewModels;

public sealed class ApiClient(string baseUrl)
{
    private HttpClient httpClient = new() { BaseAddress = new Uri(baseUrl) };

    public void Reconnect(string newBaseUrl)
    {
        httpClient.Dispose();
        httpClient = new HttpClient { BaseAddress = new Uri(newBaseUrl) };
    }

    public async Task<WatcherConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<WatcherConfig>("/api/config", cancellationToken)
            ?? WatcherConfig.Empty;
    }

    public async Task SaveConfigAsync(WatcherConfig config, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync("/api/config", config, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DaemonStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<DaemonStatusDto>("/api/status", cancellationToken)
            ?? new DaemonStatusDto();
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<AuditLogEntry>>("/api/logs?limit=500", cancellationToken)
            ?? [];
    }

    public async Task<BackfillReport> DryRunBackfillAsync(BackfillRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/backfill/dry-run", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BackfillReport>(cancellationToken)
            ?? new BackfillReport();
    }

    public async Task<IReadOnlyList<FolderActionResult>> CommitBackfillAsync(
        BackfillReport report,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/backfill/commit", report, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<FolderActionResult>>(cancellationToken)
            ?? [];
    }

    public static string LogsToCsv(IEnumerable<AuditLogEntry> logs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TimestampUtc,EventType,ScopeName,ProjectPath,Trigger,FoldersCreated,FoldersAlreadyPresent,IsError,Message");

        foreach (var log in logs)
        {
            builder.AppendLine(string.Join(",", [
                Escape(log.TimestampUtc.ToString("O")),
                Escape(log.EventType.ToString()),
                Escape(log.ScopeName ?? string.Empty),
                Escape(log.ProjectPath ?? string.Empty),
                Escape(log.Trigger),
                Escape(string.Join(";", log.FoldersCreated)),
                Escape(string.Join(";", log.FoldersAlreadyPresent)),
                Escape(log.IsError.ToString()),
                Escape(log.Message ?? string.Empty)
            ]));
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
