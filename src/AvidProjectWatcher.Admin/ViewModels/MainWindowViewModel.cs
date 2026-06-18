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

using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Backfill;
using AvidProjectWatcher.Core.Configuration;
using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Templates;

namespace AvidProjectWatcher.Admin.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly PreferencesStore preferencesStore = new();
    private readonly ApiClient apiClient;
    private readonly CancellationTokenSource pollingLifetime = new();
    private ScopeEditorViewModel? selectedScope;
    private BackfillReport? lastBackfillReport;
    private string statusText = "Connecting to daemon...";
    private string backfillSummary = "No dry run yet.";
    private bool isDaemonConnected;
    private bool isDaemonShuttingDown;
    private string daemonStatusLabel = "Connecting...";
    private bool isBackfillPanelOpen;
    private bool isLogsPanelOpen;
    private bool isSettingsPanelOpen;
    private bool isDirty;
    private bool isLoading;
    private string daemonUrlSetting;
    private bool hasUpdateAvailable;
    private string updateUrl = string.Empty;
    private bool updateCheckDone;
    private bool isDisposed;

    public MainWindowViewModel()
    {
        var prefs = preferencesStore.Load();
        apiClient = new ApiClient(prefs.DaemonBaseUrl);
        daemonUrlSetting = prefs.DaemonBaseUrl;

        AddScopeCommand = new RelayCommand(AddScope);
        RemoveScopeCommand = new RelayCommand(RemoveSelectedScope, () => SelectedScope is not null);
        AddFolderCommand = new RelayCommand(AddFolder, () => HasSelectedRootPath);
        RemoveFolderCommand = new RelayCommand(RemoveFolder);
        AddExclusionCommand = new RelayCommand(AddExclusion, () => HasSelectedRootPath);
        RemoveExclusionCommand = new RelayCommand(RemoveExclusion);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        RunBackfillDryRunCommand = new AsyncRelayCommand(RunBackfillDryRunAsync, () => HasSelectedRootPath);
        RunBackfillAllDryRunCommand = new AsyncRelayCommand(RunBackfillAllDryRunAsync, () => HasSelectedRootPath);
        CommitBackfillCommand = new AsyncRelayCommand(CommitBackfillAsync, () => lastBackfillReport?.AffectedProjectCount > 0);
        RefreshLogsCommand = new AsyncRelayCommand(RefreshLogsAsync);
        OpenBackfillPanelCommand = new RelayCommand(OpenBackfillPanel, () => HasSelectedRootPath);
        OpenLogsPanelCommand = new AsyncRelayCommand(OpenLogsPanelAsync);
        OpenSettingsPanelCommand = new RelayCommand(OpenSettingsPanel);
        ClosePanelCommand = new RelayCommand(ClosePanels);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        StopDaemonCommand = new AsyncRelayCommand(StopDaemonAsync, () => IsDaemonConnected && !IsDaemonShuttingDown);
        RestartDaemonCommand = new AsyncRelayCommand(RestartDaemonAsync, () => IsDaemonConnected && !IsDaemonShuttingDown);

        _ = CheckForUpdateAsync();

        Scopes.CollectionChanged += (_, args) =>
        {
            if (!isLoading) IsDirty = true;

            if (args.NewItems is not null)
                foreach (ScopeEditorViewModel scope in args.NewItems)
                    SubscribeToScope(scope);

            if (args.OldItems is not null)
                foreach (ScopeEditorViewModel scope in args.OldItems)
                    UnsubscribeFromScope(scope);
        };

        BackfillPlans.CollectionChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(HasBackfillPlans));
            RaisePropertyChanged(nameof(HasNoBackfillPlans));
        };
        Logs.CollectionChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(HasLogs));
            RaisePropertyChanged(nameof(HasNoLogs));
        };
    }

    public ObservableCollection<ScopeEditorViewModel> Scopes { get; } = [];

    public ObservableCollection<FolderActionPlan> BackfillPlans { get; } = [];

    public ObservableCollection<AuditLogEntry> Logs { get; } = [];

    public ScopeEditorViewModel? SelectedScope
    {
        get => selectedScope;
        set
        {
            if (ReferenceEquals(selectedScope, value))
            {
                return;
            }

            if (selectedScope is not null)
            {
                selectedScope.PropertyChanged -= SelectedScope_PropertyChanged;
            }

            if (SetProperty(ref selectedScope, value))
            {
                if (selectedScope is not null)
                {
                    selectedScope.PropertyChanged += SelectedScope_PropertyChanged;
                }

                RaisePropertyChanged(nameof(HasSelectedRootPath));
                RaisePropertyChanged(nameof(HasSelectedScope));
                RaisePropertyChanged(nameof(HasNoSelectedScope));
                RaiseCommandStates();
            }
        }
    }

    public bool HasSelectedRootPath => SelectedScope?.HasRootPath == true;

    public bool HasSelectedScope => selectedScope is not null;

    public bool HasNoSelectedScope => selectedScope is null;

    public bool HasBackfillPlans => BackfillPlans.Count > 0;

    public bool HasNoBackfillPlans => BackfillPlans.Count == 0;

    public bool HasLogs => Logs.Count > 0;

    public bool HasNoLogs => Logs.Count == 0;

    public bool IsDirty
    {
        get => isDirty;
        private set => SetProperty(ref isDirty, value);
    }

    public bool HasUpdateAvailable
    {
        get => hasUpdateAvailable;
        private set => SetProperty(ref hasUpdateAvailable, value);
    }

    public string UpdateUrl
    {
        get => updateUrl;
        private set => SetProperty(ref updateUrl, value);
    }

    public RelayCommand OpenUpdateCommand => new(() =>
    {
        if (!string.IsNullOrEmpty(UpdateUrl))
            Process.Start(new ProcessStartInfo(UpdateUrl) { UseShellExecute = true });
    });

    private async Task CheckForUpdateAsync()
    {
        if (updateCheckDone) return;
        updateCheckDone = true;

        var current = AppVersion.TrimStart('v');
        if (current is "0.0.0" or "dev") return;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AvidProjectWatcher");

            var release = await http.GetFromJsonAsync<JsonElement>(
                "https://api.github.com/repos/markbattistella/avid-project-watcher/releases/latest");

            var tag = release.GetProperty("tag_name").GetString()?.TrimStart('v') ?? string.Empty;
            var url = release.GetProperty("html_url").GetString() ?? string.Empty;

            if (!string.IsNullOrEmpty(tag) && IsNewerVersion(tag, current))
            {
                HasUpdateAvailable = true;
                UpdateUrl = url;
            }
        }
        catch
        {
            // Update check is best-effort, never fail visibly
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        var l = latest.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var c = current.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        for (var i = 0; i < Math.Max(l.Length, c.Length); i++)
        {
            var lv = i < l.Length ? l[i] : 0;
            var cv = i < c.Length ? c[i] : 0;
            if (lv > cv) return true;
            if (lv < cv) return false;
        }
        return false;
    }

    private void SubscribeToScope(ScopeEditorViewModel scope)
    {
        scope.PropertyChanged += OnScopePropertyChanged;
        scope.FolderTemplate.CollectionChanged += OnScopeCollectionChanged;
        scope.ExcludedPaths.CollectionChanged += OnScopeCollectionChanged;
    }

    private void UnsubscribeFromScope(ScopeEditorViewModel scope)
    {
        scope.PropertyChanged -= OnScopePropertyChanged;
        scope.FolderTemplate.CollectionChanged -= OnScopeCollectionChanged;
        scope.ExcludedPaths.CollectionChanged -= OnScopeCollectionChanged;
    }

    private void OnScopePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!isLoading && args.PropertyName is
                nameof(ScopeEditorViewModel.Name) or
                nameof(ScopeEditorViewModel.RootPath) or
                nameof(ScopeEditorViewModel.Enabled))
        {
            IsDirty = true;
        }
    }

    private void OnScopeCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
    {
        if (!isLoading) IsDirty = true;
    }

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public string BackfillSummary
    {
        get => backfillSummary;
        set => SetProperty(ref backfillSummary, value);
    }

    public bool IsDaemonConnected
    {
        get => isDaemonConnected;
        private set
        {
            if (SetProperty(ref isDaemonConnected, value))
            {
                StopDaemonCommand.RaiseCanExecuteChanged();
                RestartDaemonCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string DaemonStatusLabel
    {
        get => daemonStatusLabel;
        private set => SetProperty(ref daemonStatusLabel, value);
    }

    public string DaemonUrl => daemonUrlSetting;

    public string DaemonUrlSetting
    {
        get => daemonUrlSetting;
        set => SetProperty(ref daemonUrlSetting, value);
    }

    public AsyncRelayCommand ConnectCommand { get; }

    public AsyncRelayCommand StopDaemonCommand { get; }

    public AsyncRelayCommand RestartDaemonCommand { get; }

    public bool IsDaemonShuttingDown
    {
        get => isDaemonShuttingDown;
        private set
        {
            if (SetProperty(ref isDaemonShuttingDown, value))
            {
                StopDaemonCommand.RaiseCanExecuteChanged();
                RestartDaemonCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AppVersion { get; } = "v" + (Assembly
        .GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion
        .Split('+')[0]  // strip git hash suffix if present
        ?? "dev");

    public string Copyright { get; } = $"© {DateTime.UtcNow.Year} MB+MAB";

    public bool IsBackfillPanelOpen
    {
        get => isBackfillPanelOpen;
        set => SetProperty(ref isBackfillPanelOpen, value);
    }

    public bool IsLogsPanelOpen
    {
        get => isLogsPanelOpen;
        set => SetProperty(ref isLogsPanelOpen, value);
    }

    public bool IsSettingsPanelOpen
    {
        get => isSettingsPanelOpen;
        set => SetProperty(ref isSettingsPanelOpen, value);
    }

    public RelayCommand AddScopeCommand { get; }

    public RelayCommand RemoveScopeCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand RemoveFolderCommand { get; }

    public RelayCommand AddExclusionCommand { get; }

    public RelayCommand RemoveExclusionCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand RunBackfillDryRunCommand { get; }

    public AsyncRelayCommand RunBackfillAllDryRunCommand { get; }

    public AsyncRelayCommand CommitBackfillCommand { get; }

    public AsyncRelayCommand RefreshLogsCommand { get; }

    public RelayCommand OpenBackfillPanelCommand { get; }

    public AsyncRelayCommand OpenLogsPanelCommand { get; }

    public RelayCommand OpenSettingsPanelCommand { get; }

    public RelayCommand ClosePanelCommand { get; }

    public IReadOnlyList<AuditLogEntry> CurrentLogs => Logs.ToArray();

    public async Task LoadAsync()
    {
        isLoading = true;
        try
        {
            var config = await apiClient.GetConfigAsync();

            foreach (var existing in Scopes) { UnsubscribeFromScope(existing); }
            Scopes.Clear();

            foreach (var scope in config.WatchedLocations)
            {
                Scopes.Add(ScopeEditorViewModel.FromModel(scope));
            }

            SelectedScope = Scopes.FirstOrDefault();
            await RefreshStatusAsync();
            await RefreshLogsAsync();
            IsDirty = false;
        }
        catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
        {
            IsDaemonConnected = false;
            DaemonStatusLabel = "Not connected";
            StatusText = "Daemon not reachable.";
        }
        finally
        {
            isLoading = false;
        }
    }

    public async Task SaveAsync()
    {
        if (await ApplyConfigAsync())
        {
            StatusText = "Saved.";
            IsDirty = false;
        }
    }

    private async Task<bool> ApplyConfigAsync()
    {
        var config = BuildConfig();
        var validationErrors = ValidateConfig(config);
        if (validationErrors.Count > 0)
        {
            StatusText = string.Join(" ", validationErrors);
            return false;
        }

        try
        {
            await apiClient.SaveConfigAsync(config);
            await RefreshStatusAsync();
            return true;
        }
        catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
        {
            IsDaemonConnected = false;
            DaemonStatusLabel = "Not connected";
            StatusText = "Could not apply changes. Daemon not reachable.";
            return false;
        }
    }

    public async Task RefreshStatusAsync()
    {
        try
        {
            var status = await apiClient.GetStatusAsync();
            var runningCount = status.Watchers.Count(watcher => watcher.IsRunning);
            var disconnectedCount = status.Watchers.Count(watcher => watcher.IsDisconnected);
            var duplicateCount = status.DuplicateWarnings.Count;

            IsDaemonConnected = true;
            IsDaemonShuttingDown = status.IsShuttingDown;
            DaemonStatusLabel = $"{runningCount} running";

            if (status.IsShuttingDown)
            {
                StatusText = "Daemon is shutting down…";
            }
            else
            {
                StatusText = duplicateCount > 0
                    ? $"{runningCount} running · {disconnectedCount} disconnected · {duplicateCount} duplicate warning(s)"
                    : disconnectedCount > 0
                    ? $"{runningCount} running · {disconnectedCount} disconnected"
                    : runningCount == 1
                    ? "1 watcher running"
                    : $"{runningCount} watchers running";
            }
        }
        catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
        {
            IsDaemonConnected = false;
            IsDaemonShuttingDown = false;
            DaemonStatusLabel = "Not connected";
            StatusText = "Daemon not reachable.";
        }
    }

    public void AddScope()
    {
        var scope = new ScopeEditorViewModel
        {
            Name = $"Watch Folder {Scopes.Count + 1}",
            Enabled = true
        };

        Scopes.Add(scope);
        SelectedScope = scope;
    }

    public void RemoveSelectedScope()
    {
        if (SelectedScope is null)
        {
            return;
        }

        RemoveScope(SelectedScope);
    }

    public void RemoveScope(ScopeEditorViewModel scope)
    {
        var index = Scopes.IndexOf(scope);
        if (index < 0)
        {
            return;
        }

        Scopes.Remove(scope);
        SelectedScope = Scopes.Count == 0
            ? null
            : Scopes.ElementAtOrDefault(Math.Clamp(index, 0, Scopes.Count - 1));
    }

    public void AddFolder()
    {
        if (!HasSelectedRootPath || SelectedScope is null || string.IsNullOrWhiteSpace(SelectedScope.NewFolderName))
        {
            return;
        }

        var folderName = SelectedScope.NewFolderName.Trim();
        var template = SelectedScope.FolderTemplate
            .Select(folder => new FolderTemplateEntry(folder.Name))
            .Append(new FolderTemplateEntry(folderName));
        var validation = FolderTemplateValidator.ValidateFlatTemplate(template);
        if (!validation.IsValid)
        {
            StatusText = validation.Errors[0];
            return;
        }

        SelectedScope.FolderTemplate.Add(new FolderTemplateItemViewModel(folderName));
        SelectedScope.NewFolderName = string.Empty;
    }

    public void RemoveFolder()
    {
        if (SelectedScope?.SelectedFolder is null)
        {
            return;
        }

        RemoveFolder(SelectedScope.SelectedFolder);
    }

    public void RemoveFolder(FolderTemplateItemViewModel folder)
    {
        SelectedScope?.FolderTemplate.Remove(folder);
    }

    public void AddExclusion()
    {
        if (!HasSelectedRootPath || SelectedScope is null || string.IsNullOrWhiteSpace(SelectedScope.NewExcludedPath))
        {
            return;
        }

        AddExclusionPath(SelectedScope.NewExcludedPath);
        SelectedScope.NewExcludedPath = string.Empty;
    }

    public void AddExclusionPath(string path)
    {
        if (!HasSelectedRootPath || SelectedScope is null || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!SelectedScope.ExcludedPaths.Contains(path))
        {
            SelectedScope.ExcludedPaths.Add(path);
        }
    }

    public void RemoveExclusion()
    {
        if (SelectedScope?.SelectedExcludedPath is null)
        {
            return;
        }

        RemoveExclusionPath(SelectedScope.SelectedExcludedPath);
    }

    public void RemoveExclusionPath(string path)
    {
        SelectedScope?.ExcludedPaths.Remove(path);
    }

    public async Task RunBackfillDryRunAsync()
    {
        if (SelectedScope is null)
        {
            StatusText = "Select a watch folder first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedScope.RootPath))
        {
            StatusText = "Set a root path first.";
            return;
        }

        await RunBackfillDryRunAsync([SelectedScope.Id], "selected watch folder");
    }

    public async Task RunBackfillAllDryRunAsync()
    {
        if (Scopes.Count == 0)
        {
            StatusText = "Add a watch folder first.";
            return;
        }

        await RunBackfillDryRunAsync([], "all watch folders");
    }

    private async Task RunBackfillDryRunAsync(IReadOnlyList<Guid> scopeIds, string label)
    {
        if (!await ApplyConfigAsync())
        {
            return;
        }

        try
        {
            BackfillPlans.Clear();
            BackfillSummary = $"Scanning {label}...";
            StatusText = BackfillSummary;
            lastBackfillReport = await apiClient.DryRunBackfillAsync(new BackfillRequest { ScopeIds = scopeIds });
            foreach (var plan in lastBackfillReport.Plans)
            {
                BackfillPlans.Add(plan);
            }

            BackfillSummary = $"Dry run complete. Scanned {lastBackfillReport.ScannedProjectCount} project(s); {lastBackfillReport.AffectedProjectCount} need folders.";
            StatusText = BackfillSummary;
            CommitBackfillCommand.RaiseCanExecuteChanged();
        }
        catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
        {
            IsDaemonConnected = false;
            DaemonStatusLabel = "Not connected";
            StatusText = "Could not run dry run. Daemon not reachable.";
        }
    }

    public async Task CommitBackfillAsync()
    {
        if (lastBackfillReport is null)
        {
            return;
        }

        try
        {
            var results = await apiClient.CommitBackfillAsync(lastBackfillReport);
            BackfillSummary = $"Done. Folders created for {results.Count} project(s).";
            StatusText = BackfillSummary;
            lastBackfillReport = null;
            BackfillPlans.Clear();
            CommitBackfillCommand.RaiseCanExecuteChanged();
            await RefreshLogsAsync();
        }
        catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
        {
            IsDaemonConnected = false;
            DaemonStatusLabel = "Not connected";
            StatusText = "Could not commit. Daemon not reachable.";
        }
    }

    public async Task RefreshLogsAsync()
    {
        try
        {
            Logs.Clear();
            foreach (var log in await apiClient.GetLogsAsync())
            {
                Logs.Add(log);
            }
        }
        catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
        {
            IsDaemonConnected = false;
            DaemonStatusLabel = "Not connected";
            StatusText = "Could not load logs. Daemon not reachable.";
        }
    }

    private async Task ConnectAsync()
    {
        var url = DaemonUrlSetting.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusText = "Enter a daemon URL first.";
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            StatusText = "That doesn't look like a valid URL.";
            return;
        }

        preferencesStore.Save(new Preferences { DaemonBaseUrl = url });
        apiClient.Reconnect(url);
        RaisePropertyChanged(nameof(DaemonUrl));
        ClosePanels();
        await LoadAsync();
    }

    private void OpenBackfillPanel()
    {
        ClosePanels();
        IsBackfillPanelOpen = true;
    }

    private async Task OpenLogsPanelAsync()
    {
        ClosePanels();
        IsLogsPanelOpen = true;
        await RefreshLogsAsync();
    }

    private void OpenSettingsPanel()
    {
        ClosePanels();
        IsSettingsPanelOpen = true;
    }

    private void ClosePanels()
    {
        IsBackfillPanelOpen = false;
        IsLogsPanelOpen = false;
        IsSettingsPanelOpen = false;
    }

    public WatcherConfig BuildConfig()
    {
        return new WatcherConfig
        {
            WatchedLocations = Scopes.Select(scope => scope.ToModel()).ToArray()
        };
    }

    private static IReadOnlyList<string> ValidateConfig(WatcherConfig config)
    {
        var errors = new List<string>();

        foreach (var scope in config.WatchedLocations)
        {
            if (string.IsNullOrWhiteSpace(scope.Name))
            {
                errors.Add("Every watch folder needs a name.");
            }

            if (string.IsNullOrWhiteSpace(scope.RootPath))
            {
                errors.Add($"Watch folder '{scope.Name}' needs a root path.");
            }

            if (scope.FolderTemplate.Count == 0)
            {
                errors.Add($"Watch folder '{scope.Name}' needs at least one folder template entry.");
            }

            var templateValidation = FolderTemplateValidator.ValidateFlatTemplate(scope.FolderTemplate);
            errors.AddRange(templateValidation.Errors.Select(error => $"{scope.Name}: {error}"));
        }

        return errors;
    }

    private void RaiseCommandStates()
    {
        RemoveScopeCommand.RaiseCanExecuteChanged();
        AddFolderCommand.RaiseCanExecuteChanged();
        RemoveFolderCommand.RaiseCanExecuteChanged();
        AddExclusionCommand.RaiseCanExecuteChanged();
        RemoveExclusionCommand.RaiseCanExecuteChanged();
        RunBackfillDryRunCommand.RaiseCanExecuteChanged();
        RunBackfillAllDryRunCommand.RaiseCanExecuteChanged();
        OpenBackfillPanelCommand.RaiseCanExecuteChanged();
    }

    private void SelectedScope_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ScopeEditorViewModel.RootPath) or nameof(ScopeEditorViewModel.HasRootPath))
        {
            RaisePropertyChanged(nameof(HasSelectedRootPath));
            RaiseCommandStates();
        }
    }

    public void StartPolling()
    {
        _ = PollAsync(pollingLifetime.Token);
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (isDaemonConnected)
                {
                    await RefreshStatusAsync();
                }
                else
                {
                    try
                    {
                        if (await apiClient.CheckHealthAsync(cancellationToken))
                        {
                            await LoadAsync();
                        }
                    }
                    catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
                    {
                    }
                }
            });
        }
    }

    private async Task StopDaemonAsync()
    {
        try
        {
            await apiClient.StopDaemonAsync();
            StatusText = "Daemon is stopping…";
            IsDaemonShuttingDown = true;
        }
        catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
        {
            IsDaemonConnected = false;
            IsDaemonShuttingDown = false;
            DaemonStatusLabel = "Not connected";
            StatusText = "Could not stop daemon. Not reachable.";
        }
    }

    private async Task RestartDaemonAsync()
    {
        try
        {
            await apiClient.RestartDaemonAsync();
            StatusText = "Daemon is restarting…";
            IsDaemonShuttingDown = true;
        }
        catch (Exception exception) when (ApiClient.IsConnectionFailure(exception))
        {
            IsDaemonConnected = false;
            IsDaemonShuttingDown = false;
            DaemonStatusLabel = "Not connected";
            StatusText = "Could not restart daemon. Not reachable.";
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        pollingLifetime.Cancel();
        pollingLifetime.Dispose();
        apiClient.Dispose();
    }
}
