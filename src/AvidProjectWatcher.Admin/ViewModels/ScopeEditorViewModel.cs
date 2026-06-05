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

    public ScopeEditorViewModel()
    {
        FolderTemplate.CollectionChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(HasFolderTemplate));
            RaisePropertyChanged(nameof(HasNoFolderTemplate));
        };
        ExcludedPaths.CollectionChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(HasExcludedPaths));
            RaisePropertyChanged(nameof(HasNoExcludedPaths));
        };
    }

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
                RaisePropertyChanged(nameof(RootPathWarning));
                RaisePropertyChanged(nameof(HasRootPathWarning));
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

    public string? RootPathWarning
    {
        get
        {
            var trimmed = RootPath.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return null;

            // UNC network path: \\server\share or //server/share
            if (trimmed.StartsWith(@"\\") || trimmed.StartsWith("//")) return null;
            // Windows drive letter: C:\... or mapped drive Z:\...
            if (trimmed.Length >= 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':') return null;
            // Unix/macOS absolute path: /Volumes/... or /mnt/...
            if (trimmed.StartsWith('/')) return null;

            return @"This doesn't look like a valid path. Use a network share (\\server\share), a drive letter (C:\) or a mount point (/).";
        }
    }

    public bool HasRootPathWarning => RootPathWarning is not null;

    public bool HasFolderTemplate => FolderTemplate.Count > 0;
    public bool HasNoFolderTemplate => FolderTemplate.Count == 0;
    public bool HasExcludedPaths => ExcludedPaths.Count > 0;
    public bool HasNoExcludedPaths => ExcludedPaths.Count == 0;

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
