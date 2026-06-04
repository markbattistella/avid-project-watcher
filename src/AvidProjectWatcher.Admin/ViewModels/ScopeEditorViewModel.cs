using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Admin.ViewModels;

public sealed class ScopeEditorViewModel : ViewModelBase
{
    private string name = string.Empty;
    private string rootPath = string.Empty;
    private bool enabled = true;
    private string newFolderName = string.Empty;
    private string? selectedFolder;
    private string newExcludedPath = string.Empty;
    private string? selectedExcludedPath;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public string RootPath
    {
        get => rootPath;
        set => SetProperty(ref rootPath, value);
    }

    public bool Enabled
    {
        get => enabled;
        set => SetProperty(ref enabled, value);
    }

    public ObservableCollection<string> FolderTemplate { get; } = [];

    public ObservableCollection<string> ExcludedPaths { get; } = [];

    public string NewFolderName
    {
        get => newFolderName;
        set => SetProperty(ref newFolderName, value);
    }

    public string? SelectedFolder
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
            viewModel.FolderTemplate.Add(folder.RelativePath);
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
            Name = Name.Trim(),
            RootPath = RootPath.Trim(),
            Enabled = Enabled,
            FolderTemplate = FolderTemplate
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Select(folder => new FolderTemplateEntry(folder.Trim()))
                .ToArray(),
            ExcludedPaths = ExcludedPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new ExcludedPath(path.Trim()))
                .ToArray()
        };
    }
}
