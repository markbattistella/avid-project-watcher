using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvidProjectWatcher.Admin.ViewModels;

namespace AvidProjectWatcher.Admin.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Opened += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void BrowseRoot_Click(object? sender, RoutedEventArgs args)
    {
        if (ViewModel.SelectedScope is null)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select project scope root",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            ViewModel.SelectedScope.RootPath = folder.Path.LocalPath;
        }
    }

    private async void BrowseExclusion_Click(object? sender, RoutedEventArgs args)
    {
        if (ViewModel.SelectedScope is null)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder to exclude",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            ViewModel.AddExclusionPath(folder.Path.LocalPath);
        }
    }

    private void Exclusions_Drop(object? sender, DragEventArgs args)
    {
        if (ViewModel.SelectedScope is null)
        {
            return;
        }

        var files = args.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return;
        }

        foreach (var file in files)
        {
            if (file is IStorageFolder folder)
            {
                ViewModel.AddExclusionPath(folder.Path.LocalPath);
            }
        }
    }

    private async void ExportLogs_Click(object? sender, RoutedEventArgs args)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export audit logs",
            SuggestedFileName = $"avid-project-watcher-log-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.csv",
            FileTypeChoices =
            [
                new FilePickerFileType("CSV") { Patterns = ["*.csv"] }
            ]
        });

        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(ApiClient.LogsToCsv(ViewModel.CurrentLogs));
    }
}
