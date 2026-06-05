using System.ComponentModel;
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
    private bool isBackfillPanelOpen;
    private bool isLogsPanelOpen;
    private bool isSettingsPanelOpen;

    public MainWindowViewModel()
    {
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
        OpenLogsPanelCommand = new AsyncRelayCommand(OpenLogsPanelAsync, () => HasSelectedRootPath);
        OpenSettingsPanelCommand = new RelayCommand(OpenSettingsPanel);
        ClosePanelCommand = new RelayCommand(ClosePanels);
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
                RaiseCommandStates();
            }
        }
    }

    public bool HasSelectedRootPath => SelectedScope?.HasRootPath == true;

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
        if (await ApplyConfigAsync())
        {
            StatusText = "Changes applied.";
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
        catch (HttpRequestException exception)
        {
            StatusText = $"Could not apply changes: {exception.Message}";
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
            StatusText = "Select a watch folder before running a selected-folder dry run.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedScope.RootPath))
        {
            StatusText = "Choose a root path before running backfill.";
            return;
        }

        await RunBackfillDryRunAsync([SelectedScope.Id], "selected watch folder");
    }

    public async Task RunBackfillAllDryRunAsync()
    {
        if (Scopes.Count == 0)
        {
            StatusText = "Add a watch folder before running backfill.";
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
            lastBackfillReport = await apiClient.DryRunBackfillAsync(new BackfillRequest { ScopeIds = scopeIds });
            BackfillPlans.Clear();
            foreach (var plan in lastBackfillReport.Plans)
            {
                BackfillPlans.Add(plan);
            }

            BackfillSummary = $"Dry run for {label} found {lastBackfillReport.AffectedProjectCount} affected project(s).";
            StatusText = BackfillSummary;
            CommitBackfillCommand.RaiseCanExecuteChanged();
        }
        catch (HttpRequestException exception)
        {
            StatusText = $"Could not run backfill dry run: {exception.Message}";
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
            BackfillSummary = $"Committed backfill for {results.Count} project(s).";
            StatusText = BackfillSummary;
            lastBackfillReport = null;
            BackfillPlans.Clear();
            CommitBackfillCommand.RaiseCanExecuteChanged();
            await RefreshLogsAsync();
        }
        catch (HttpRequestException exception)
        {
            StatusText = $"Could not commit backfill: {exception.Message}";
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
        catch (HttpRequestException exception)
        {
            StatusText = $"Could not load logs: {exception.Message}";
        }
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
        OpenLogsPanelCommand.RaiseCanExecuteChanged();
    }

    private void SelectedScope_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ScopeEditorViewModel.RootPath) or nameof(ScopeEditorViewModel.HasRootPath))
        {
            RaisePropertyChanged(nameof(HasSelectedRootPath));
            RaiseCommandStates();
        }
    }
}
