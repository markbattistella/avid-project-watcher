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

public sealed class ApiClient(string baseUrl) : IDisposable
{
    private static readonly TimeSpan ShortRequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WriteRequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BackfillRequestTimeout = TimeSpan.FromMinutes(30);
    private HttpClient httpClient = CreateHttpClient(baseUrl);
    private CancellationTokenSource requestLifetime = new();

    public void Reconnect(string newBaseUrl)
    {
        requestLifetime.Cancel();
        requestLifetime.Dispose();
        httpClient.Dispose();
        requestLifetime = new CancellationTokenSource();
        httpClient = CreateHttpClient(newBaseUrl);
    }

    public async Task<WatcherConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(ShortRequestTimeout, cancellationToken);
        return await httpClient.GetFromJsonAsync<WatcherConfig>("/api/config", timeout.Token)
            ?? WatcherConfig.Empty;
    }

    public async Task SaveConfigAsync(WatcherConfig config, CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(WriteRequestTimeout, cancellationToken);
        using var response = await httpClient.PutAsJsonAsync("/api/config", config, timeout.Token);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DaemonStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(ShortRequestTimeout, cancellationToken);
        return await httpClient.GetFromJsonAsync<DaemonStatusDto>("/api/status", timeout.Token)
            ?? new DaemonStatusDto();
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(ShortRequestTimeout, cancellationToken);
        return await httpClient.GetFromJsonAsync<IReadOnlyList<AuditLogEntry>>("/api/logs?limit=500", timeout.Token)
            ?? [];
    }

    public async Task<BackfillReport> DryRunBackfillAsync(BackfillRequest request, CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(BackfillRequestTimeout, cancellationToken);
        using var response = await httpClient.PostAsJsonAsync("/api/backfill/dry-run", request, timeout.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BackfillReport>(timeout.Token)
            ?? new BackfillReport();
    }

    public async Task<IReadOnlyList<FolderActionResult>> CommitBackfillAsync(
        BackfillReport report,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(BackfillRequestTimeout, cancellationToken);
        using var response = await httpClient.PostAsJsonAsync("/api/backfill/commit", report, timeout.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<FolderActionResult>>(timeout.Token)
            ?? [];
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(ShortRequestTimeout, cancellationToken);
        using var response = await httpClient.GetAsync("/health", timeout.Token);
        return response.IsSuccessStatusCode;
    }

    public async Task StopDaemonAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(WriteRequestTimeout, cancellationToken);
        using var response = await httpClient.PostAsync("/api/control/stop", null, timeout.Token);
        response.EnsureSuccessStatusCode();
    }

    public async Task RestartDaemonAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(WriteRequestTimeout, cancellationToken);
        using var response = await httpClient.PostAsync("/api/control/restart", null, timeout.Token);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        requestLifetime.Cancel();
        requestLifetime.Dispose();
        httpClient.Dispose();
    }

    public static bool IsConnectionFailure(Exception exception)
    {
        return exception is HttpRequestException or TaskCanceledException or OperationCanceledException;
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

    private static HttpClient CreateHttpClient(string baseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private CancellationTokenSource CreateTimeout(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(
            requestLifetime.Token,
            cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }
}
