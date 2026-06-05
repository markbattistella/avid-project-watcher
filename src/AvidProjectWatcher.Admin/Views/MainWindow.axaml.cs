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

    private void RootPath_Drop(object? sender, DragEventArgs args)
    {
        if (ViewModel.SelectedScope is null) return;

        var files = args.DataTransfer.TryGetFiles();
        var folder = files?.OfType<IStorageFolder>().FirstOrDefault();
        if (folder is not null)
        {
            ViewModel.SelectedScope.RootPath = folder.Path.LocalPath;
            args.Handled = true;
        }
    }

    private async void BrowseRoot_Click(object? sender, RoutedEventArgs args)
    {
        if (ViewModel.SelectedScope is null)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select watch folder root",
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
        if (!ViewModel.HasSelectedRootPath || ViewModel.SelectedScope is null)
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
        if (!ViewModel.HasSelectedRootPath || ViewModel.SelectedScope is null)
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

    private void RemoveWatchFolder_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control { DataContext: ScopeEditorViewModel scope })
        {
            return;
        }

        ViewModel.RemoveScope(scope);
        args.Handled = true;
    }

    private void RemoveExclusionPath_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control { DataContext: string path })
        {
            return;
        }

        ViewModel.RemoveExclusionPath(path);
        args.Handled = true;
    }

    private void RemoveFolderTemplate_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control { DataContext: FolderTemplateItemViewModel folder })
        {
            return;
        }

        ViewModel.RemoveFolder(folder);
        args.Handled = true;
    }

    private void NewFolderName_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            ViewModel.AddFolder();
            args.Handled = true;
        }
    }

    private void NewExcludedPath_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            ViewModel.AddExclusion();
            args.Handled = true;
        }
    }

    private void FolderTemplateItem_DoubleTapped(object? sender, TappedEventArgs args)
    {
        BeginFolderTemplateEdit(sender);
        args.Handled = true;
    }

    private void EditFolderTemplate_Click(object? sender, RoutedEventArgs args)
    {
        BeginFolderTemplateEdit(sender);
        args.Handled = true;
    }

    private static void BeginFolderTemplateEdit(object? sender)
    {
        if (sender is Control { DataContext: FolderTemplateItemViewModel item })
        {
            item.IsEditing = true;
        }
    }

    private void FolderTemplateEdit_LostFocus(object? sender, RoutedEventArgs args)
    {
        EndFolderTemplateEdit(sender);
    }

    private void FolderTemplateEdit_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key is Key.Enter or Key.Escape)
        {
            EndFolderTemplateEdit(sender);
            args.Handled = true;
        }
    }

    private static void EndFolderTemplateEdit(object? sender)
    {
        if (sender is Control { DataContext: FolderTemplateItemViewModel item })
        {
            item.IsEditing = false;
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
