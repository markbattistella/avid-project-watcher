using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Admin.ViewModels;

public sealed class ScopeEditorViewModel : ViewModelBase
{
    private string name = string.Empty;
    private string rootPath = string.Empty;
    private bool enabled = true;
    private string newFolderName = string.Empty;
    private FolderTemplateItemViewModel? selectedFolder;
    private string newExcludedPath = string.Empty;
    private string? selectedExcludedPath;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name
    {
        get => name;
        set
        {
            if (SetProperty(ref name, value))
            {
                RaisePropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string RootPath
    {
        get => rootPath;
        set
        {
            if (SetProperty(ref rootPath, value))
            {
                RaisePropertyChanged(nameof(DisplayName));
                RaisePropertyChanged(nameof(HasRootPath));
            }
        }
    }

    public bool Enabled
    {
        get => enabled;
        set => SetProperty(ref enabled, value);
    }

    public string DisplayName => DeriveName(RootPath, Name);

    public bool HasRootPath => !string.IsNullOrWhiteSpace(RootPath);

    public ObservableCollection<FolderTemplateItemViewModel> FolderTemplate { get; } = [];

    public ObservableCollection<string> ExcludedPaths { get; } = [];

    public string NewFolderName
    {
        get => newFolderName;
        set => SetProperty(ref newFolderName, value);
    }

    public FolderTemplateItemViewModel? SelectedFolder
    {
        get => selectedFolder;
        set => SetProperty(ref selectedFolder, value);
    }

    public string NewExcludedPath
    {
        get => newExcludedPath;
        set => SetProperty(ref newExcludedPath, value);
    }

    public string? SelectedExcludedPath
    {
        get => selectedExcludedPath;
        set => SetProperty(ref selectedExcludedPath, value);
    }

    public static ScopeEditorViewModel FromModel(WatchedLocation location)
    {
        var viewModel = new ScopeEditorViewModel
        {
            Id = location.Id,
            Name = location.Name,
            RootPath = location.RootPath,
            Enabled = location.Enabled
        };

        foreach (var folder in location.FolderTemplate)
        {
            viewModel.FolderTemplate.Add(new FolderTemplateItemViewModel(folder.RelativePath));
        }

        foreach (var exclusion in location.ExcludedPaths)
        {
            viewModel.ExcludedPaths.Add(exclusion.Path);
        }

        return viewModel;
    }

    public WatchedLocation ToModel()
    {
        return new WatchedLocation
        {
            Id = Id,
            Name = DisplayName,
            RootPath = RootPath.Trim(),
            Enabled = Enabled,
            FolderTemplate = FolderTemplate
                .Select(folder => folder.Name)
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Select(folder => new FolderTemplateEntry(folder.Trim()))
                .ToArray(),
            ExcludedPaths = ExcludedPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new ExcludedPath(path.Trim()))
                .ToArray()
        };
    }

    private static string DeriveName(string rootPath, string fallback)
    {
        var trimmed = rootPath.Trim().TrimEnd('/', '\\');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "New watch folder" : fallback.Trim();
        }

        var parts = trimmed.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? trimmed : parts[^1];
    }
}
