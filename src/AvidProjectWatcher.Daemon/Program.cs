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

using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Backfill;
using AvidProjectWatcher.Core.Configuration;
using AvidProjectWatcher.Core.Discovery;
using AvidProjectWatcher.Core.Folders;
using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Projects;
using AvidProjectWatcher.Core.State;
using AvidProjectWatcher.Core.Watching;
using AvidProjectWatcher.Daemon;
using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();
builder.WebHost.UseUrls(ConfigDefaults.DefaultApiListenUrl);

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddSingleton<IConfigStore>(_ => new JsonConfigStore(ConfigDefaults.DefaultConfigPath));
builder.Services.AddSingleton<IStateStore>(_ => new JsonStateStore(ConfigDefaults.DefaultStatePath));
builder.Services.AddSingleton<IAuditLog>(_ => new SqliteAuditLog(ConfigDefaults.DefaultAuditDatabasePath));
builder.Services.AddSingleton<DaemonRuntimeState>();
builder.Services.AddSingleton<ScopeResolver>();
builder.Services.AddSingleton<ExclusionMatcher>();
builder.Services.AddSingleton<ProjectResolver>();
builder.Services.AddSingleton<ProjectScanner>();
builder.Services.AddSingleton<FolderActionPlanner>();
builder.Services.AddSingleton<FolderCreator>();
builder.Services.AddSingleton<ProjectObservationTracker>();
builder.Services.AddSingleton<BackfillEngine>();
builder.Services.AddSingleton<DuplicateWatcherDetector>();
builder.Services.AddSingleton<LiveProjectProcessor>();
builder.Services.AddSingleton(serviceProvider =>
{
    var processor = serviceProvider.GetRequiredService<LiveProjectProcessor>();
    return new WatchCoordinator(processor.HandleAvpAsync);
});
builder.Services.AddSingleton<ConfigReloader>();
builder.Services.AddHostedService<ConfigWatcherHostedService>();
builder.Services.AddHostedService<ReconciliationHostedService>();
builder.Services.AddHostedService<WatcherRecoveryHostedService>();
builder.Services.AddHostedService<LanDiscoveryHostedService>();

var app = builder.Build();

app.MapGet("/api/status", (
    DaemonRuntimeState runtime,
    WatchCoordinator coordinator,
    DuplicateWatcherDetector duplicateDetector) =>
{
    var config = runtime.CurrentConfig;
    var warnings = duplicateDetector.FindWarnings(
        runtime.InstanceId,
        config.WatchedLocations,
        runtime.GetRemoteAdvertisements());

    return Results.Ok(new DaemonStatus
    {
        InstanceId = runtime.InstanceId,
        MachineName = Environment.MachineName,
        ConfigPath = ConfigDefaults.DefaultConfigPath,
        StatePath = ConfigDefaults.DefaultStatePath,
        AuditDatabasePath = ConfigDefaults.DefaultAuditDatabasePath,
        LastConfigReloadUtc = runtime.LastConfigReloadUtc,
        Watchers = coordinator.Statuses,
        DuplicateWarnings = warnings
    });
});

app.MapGet("/api/config", async (IConfigStore configStore, CancellationToken cancellationToken) =>
{
    return Results.Ok(await configStore.LoadAsync(cancellationToken));
});

app.MapPut("/api/config", async (
    WatcherConfig config,
    IConfigStore configStore,
    ConfigReloader reloader,
    CancellationToken cancellationToken) =>
{
    await configStore.SaveAsync(config, cancellationToken);
    await reloader.ReloadAsync(cancellationToken);
    return Results.Ok(config);
});

app.MapPost("/api/config/import", async (
    ConfigImportRequest request,
    IConfigStore configStore,
    ConfigReloader reloader,
    CancellationToken cancellationToken) =>
{
    await configStore.SaveAsync(request.Config, cancellationToken);
    await reloader.ReloadAsync(cancellationToken);
    return Results.Ok(request.Config);
});

app.MapGet("/api/config/export", async (IConfigStore configStore, CancellationToken cancellationToken) =>
{
    return Results.Ok(await configStore.LoadAsync(cancellationToken));
});

app.MapGet("/api/logs", async (
    Guid? scopeId,
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    AuditEventType? eventType,
    bool? isError,
    int? limit,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    var logs = await auditLog.QueryAsync(new AuditLogQuery
    {
        ScopeId = scopeId,
        FromUtc = fromUtc,
        ToUtc = toUtc,
        EventType = eventType,
        IsError = isError,
        Limit = limit ?? 250
    }, cancellationToken);

    return Results.Ok(logs);
});

app.MapPost("/api/backfill/dry-run", async (
    BackfillRequest request,
    IConfigStore configStore,
    BackfillEngine backfillEngine,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    var config = await configStore.LoadAsync(cancellationToken);
    var report = await backfillEngine.DryRunAsync(config, request, cancellationToken);
    await auditLog.AppendAsync(new AuditLogEntry
    {
        EventType = AuditEventType.BackfillDryRun,
        Trigger = "manual-backfill",
        Message = $"Dry run found {report.AffectedProjectCount} affected project(s)."
    }, cancellationToken);

    return Results.Ok(report);
});

app.MapPost("/api/backfill/commit", async (
    BackfillReport report,
    BackfillEngine backfillEngine,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    var results = await backfillEngine.CommitAsync(report, cancellationToken);

    foreach (var result in results)
    {
        await auditLog.AppendAsync(AuditEntryFactory.FromFolderResult(result), cancellationToken);
    }

    await auditLog.AppendAsync(new AuditLogEntry
    {
        EventType = AuditEventType.BackfillCommitted,
        Trigger = "manual-backfill",
        Message = $"Committed backfill for {results.Count} project(s)."
    }, cancellationToken);

    return Results.Ok(results);
});

app.Run();
