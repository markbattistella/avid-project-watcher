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
using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Models;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace AvidProjectWatcher.Admin.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Opened += async (_, _) =>
        {
            await ViewModel.LoadAsync();
            ViewModel.StartPolling();
        };
        Closed += (_, _) => ViewModel.Dispose();
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

    private async void OpenDryRunPreview_Click(object? sender, RoutedEventArgs args)
    {
        await OpenHtmlPreviewAsync(
            "Avid Project Watcher - Dry Run",
            BuildDryRunHtml(ViewModel.BackfillSummary, ViewModel.BackfillPlans));
    }

    private async void OpenLogsPreview_Click(object? sender, RoutedEventArgs args)
    {
        await OpenHtmlPreviewAsync(
            "Avid Project Watcher - Audit Log",
            BuildLogsHtml(ViewModel.CurrentLogs));
    }

    private static async Task OpenHtmlPreviewAsync(string title, string html)
    {
        var fileName = title
            .ToLowerInvariant()
            .Replace(" - ", "-", StringComparison.Ordinal)
            .Replace(" ", "-", StringComparison.Ordinal);
        var path = Path.Combine(Path.GetTempPath(), $"{fileName}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.html");
        await File.WriteAllTextAsync(path, html, Encoding.UTF8);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static string BuildDryRunHtml(string summary, IEnumerable<FolderActionPlan> plans)
    {
        var builder = CreateHtml("Dry Run", summary);
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>Watch Folder</th><th>Project</th><th>Missing Folders</th><th>Existing</th><th>Skipped</th></tr></thead>");
        builder.AppendLine("<tbody>");

        foreach (var plan in plans)
        {
            builder.Append("<tr>");
            AppendCell(builder, plan.ScopeName);
            AppendCell(builder, plan.ProjectDirectory, "path");
            AppendCell(builder, string.Join(", ", plan.FoldersToCreate));
            AppendCell(builder, string.Join(", ", plan.FoldersAlreadyPresent));
            AppendCell(builder, plan.SkippedReason ?? string.Empty);
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table></body></html>");
        return builder.ToString();
    }

    private static string BuildLogsHtml(IEnumerable<AuditLogEntry> logs)
    {
        var builder = CreateHtml("Audit Log", "Recent daemon activity");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>UTC</th><th>Type</th><th>Watch Folder</th><th>Project</th><th>Trigger</th><th>Created</th><th>Existing</th><th>Error</th><th>Message</th></tr></thead>");
        builder.AppendLine("<tbody>");

        foreach (var log in logs)
        {
            builder.Append("<tr>");
            AppendCell(builder, log.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendCell(builder, log.EventType.ToString());
            AppendCell(builder, log.ScopeName ?? string.Empty);
            AppendCell(builder, log.ProjectPath ?? string.Empty, "path");
            AppendCell(builder, log.Trigger);
            AppendCell(builder, string.Join(", ", log.FoldersCreated));
            AppendCell(builder, string.Join(", ", log.FoldersAlreadyPresent));
            AppendCell(builder, log.IsError ? "Yes" : string.Empty);
            AppendCell(builder, log.Message ?? string.Empty);
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table></body></html>");
        return builder.ToString();
    }

    private static StringBuilder CreateHtml(string heading, string summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\">");
        builder.Append("<title>").Append(Html(heading)).AppendLine("</title>");
        builder.AppendLine("""
            <style>
            :root { color-scheme: dark; }
            body { margin: 24px; background: #0d1117; color: #e6edf3; font-family: "Segoe UI", Arial, sans-serif; font-size: 14px; }
            h1 { margin: 0 0 6px; font-size: 24px; }
            p { margin: 0 0 18px; color: #8b949e; }
            table { width: 100%; border-collapse: collapse; table-layout: auto; }
            th, td { border-bottom: 1px solid #30363d; padding: 8px 10px; text-align: left; vertical-align: top; }
            th { position: sticky; top: 0; background: #161b22; color: #f0f6fc; z-index: 1; }
            tr:hover td { background: #161b22; }
            td.path { font-family: Consolas, "Courier New", monospace; white-space: nowrap; }
            td { max-width: 560px; overflow-wrap: anywhere; }
            </style>
            """);
        builder.AppendLine("</head><body>");
        builder.Append("<h1>").Append(Html(heading)).AppendLine("</h1>");
        builder.Append("<p>").Append(Html(summary)).AppendLine("</p>");
        return builder;
    }

    private static void AppendCell(StringBuilder builder, string value, string? cssClass = null)
    {
        builder.Append("<td");
        if (!string.IsNullOrWhiteSpace(cssClass))
        {
            builder.Append(" class=\"").Append(Html(cssClass)).Append('"');
        }

        builder.Append('>').Append(Html(value)).Append("</td>");
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
