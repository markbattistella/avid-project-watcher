using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Backfill;
using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Templates;

namespace AvidProjectWatcher.Admin.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ApiClient apiClient = new();
    private ScopeEditorViewModel? selectedScope;
    private BackfillReport? lastBackfillReport;
    private string statusText = "Daemon not connected.";
    private string backfillSummary = "No dry run has been run.";

    public MainWindowViewModel()
    {
        AddScopeCommand = new RelayCommand(AddScope);
        RemoveScopeCommand = new RelayCommand(RemoveSelectedScope, () => SelectedScope is not null);
        AddFolderCommand = new RelayCommand(AddFolder, () => SelectedScope is not null);
        RemoveFolderCommand = new RelayCommand(RemoveFolder);
        AddExclusionCommand = new RelayCommand(AddExclusion, () => SelectedScope is not null);
        RemoveExclusionCommand = new RelayCommand(RemoveExclusion);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        RunBackfillDryRunCommand = new AsyncRelayCommand(RunBackfillDryRunAsync);
        CommitBackfillCommand = new AsyncRelayCommand(CommitBackfillAsync, () => lastBackfillReport?.AffectedProjectCount > 0);
        RefreshLogsCommand = new AsyncRelayCommand(RefreshLogsAsync);
    }

    public ObservableCollection<ScopeEditorViewModel> Scopes { get; } = [];

    public ObservableCollection<FolderActionPlan> BackfillPlans { get; } = [];

    public ObservableCollection<AuditLogEntry> Logs { get; } = [];

    public ScopeEditorViewModel? SelectedScope
    {
        get => selectedScope;
        set
        {
            if (SetProperty(ref selectedScope, value))
            {
                RaiseCommandStates();
            }
        }
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

    public RelayCommand AddScopeCommand { get; }

    public RelayCommand RemoveScopeCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand RemoveFolderCommand { get; }

    public RelayCommand AddExclusionCommand { get; }

    public RelayCommand RemoveExclusionCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand RunBackfillDryRunCommand { get; }

    public AsyncRelayCommand CommitBackfillCommand { get; }

    public AsyncRelayCommand RefreshLogsCommand { get; }

    public IReadOnlyList<AuditLogEntry> CurrentLogs => Logs.ToArray();

    public async Task LoadAsync()
    {
        try
        {
            var config = await apiClient.GetConfigAsync();
            Scopes.Clear();
            foreach (var scope in config.WatchedLocations)
            {
                Scopes.Add(ScopeEditorViewModel.FromModel(scope));
            }

            SelectedScope = Scopes.FirstOrDefault();
            await RefreshStatusAsync();
            await RefreshLogsAsync();
        }
        catch (HttpRequestException exception)
        {
            StatusText = $"Daemon not reachable: {exception.Message}";
        }
    }

    public async Task SaveAsync()
    {
        var config = BuildConfig();
        var validationErrors = ValidateConfig(config);
        if (validationErrors.Count > 0)
        {
            StatusText = string.Join(" ", validationErrors);
            return;
        }

        await apiClient.SaveConfigAsync(config);
        await RefreshStatusAsync();
        StatusText = "Configuration saved.";
    }

    public async Task RefreshStatusAsync()
    {
        try
        {
            var status = await apiClient.GetStatusAsync();
            var runningCount = status.Watchers.Count(watcher => watcher.IsRunning);
            var disconnectedCount = status.Watchers.Count(watcher => watcher.IsDisconnected);
            var duplicateCount = status.DuplicateWarnings.Count;
            StatusText = $"{runningCount} watcher(s) running, {disconnectedCount} disconnected, {duplicateCount} duplicate warning(s). Config: {status.ConfigPath}";
        }
        catch (HttpRequestException exception)
        {
            StatusText = $"Daemon not reachable: {exception.Message}";
        }
    }

    public void AddScope()
    {
        var scope = new ScopeEditorViewModel
        {
            Name = $"Scope {Scopes.Count + 1}",
            Enabled = true
        };

        scope.FolderTemplate.Add("FOOTAGE");
        scope.FolderTemplate.Add("SEQUENCES");
        scope.FolderTemplate.Add("SFX");
        scope.FolderTemplate.Add("GFX");
        Scopes.Add(scope);
        SelectedScope = scope;
    }

    public void RemoveSelectedScope()
    {
        if (SelectedScope is null)
        {
            return;
        }

        var index = Scopes.IndexOf(SelectedScope);
        Scopes.Remove(SelectedScope);
        SelectedScope = Scopes.Count == 0
            ? null
            : Scopes.ElementAtOrDefault(Math.Clamp(index, 0, Scopes.Count - 1));
    }

    public void AddFolder()
    {
        if (SelectedScope is null || string.IsNullOrWhiteSpace(SelectedScope.NewFolderName))
        {
            return;
        }

        SelectedScope.FolderTemplate.Add(SelectedScope.NewFolderName.Trim());
        SelectedScope.NewFolderName = string.Empty;
    }

    public void RemoveFolder()
    {
        if (SelectedScope?.SelectedFolder is null)
        {
            return;
        }

        SelectedScope.FolderTemplate.Remove(SelectedScope.SelectedFolder);
    }

    public void AddExclusion()
    {
        if (SelectedScope is null || string.IsNullOrWhiteSpace(SelectedScope.NewExcludedPath))
        {
            return;
        }

        AddExclusionPath(SelectedScope.NewExcludedPath);
        SelectedScope.NewExcludedPath = string.Empty;
    }

    public void AddExclusionPath(string path)
    {
        if (SelectedScope is null || string.IsNullOrWhiteSpace(path))
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

        SelectedScope.ExcludedPaths.Remove(SelectedScope.SelectedExcludedPath);
    }

    public async Task RunBackfillDryRunAsync()
    {
        var request = new BackfillRequest
        {
            ScopeIds = SelectedScope is null ? [] : [SelectedScope.Id]
        };

        lastBackfillReport = await apiClient.DryRunBackfillAsync(request);
        BackfillPlans.Clear();
        foreach (var plan in lastBackfillReport.Plans)
        {
            BackfillPlans.Add(plan);
        }

        BackfillSummary = $"Dry run found {lastBackfillReport.AffectedProjectCount} affected project(s).";
        CommitBackfillCommand.RaiseCanExecuteChanged();
    }

    public async Task CommitBackfillAsync()
    {
        if (lastBackfillReport is null)
        {
            return;
        }

        var results = await apiClient.CommitBackfillAsync(lastBackfillReport);
        BackfillSummary = $"Committed backfill for {results.Count} project(s).";
        lastBackfillReport = null;
        BackfillPlans.Clear();
        CommitBackfillCommand.RaiseCanExecuteChanged();
        await RefreshLogsAsync();
    }

    public async Task RefreshLogsAsync()
    {
        Logs.Clear();
        foreach (var log in await apiClient.GetLogsAsync())
        {
            Logs.Add(log);
        }
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
                errors.Add("Every scope needs a name.");
            }

            if (string.IsNullOrWhiteSpace(scope.RootPath))
            {
                errors.Add($"Scope '{scope.Name}' needs a root path.");
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
    }
}
