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

using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Core.Folders;

public sealed class FolderCreator
{
    public Task<FolderActionResult> ApplyAsync(FolderActionPlan plan, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(plan.SkippedReason))
        {
            return Task.FromResult(new FolderActionResult
            {
                WatchedLocationId = plan.WatchedLocationId,
                ScopeName = plan.ScopeName,
                ProjectDirectory = plan.ProjectDirectory,
                FoldersAlreadyPresent = plan.FoldersAlreadyPresent,
                Errors = [plan.SkippedReason],
                Source = plan.Source
            });
        }

        var created = new List<string>();
        var errors = new List<string>();

        foreach (var folderName in plan.FoldersToCreate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Directory.CreateDirectory(Path.Combine(plan.ProjectDirectory, folderName));
                created.Add(folderName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{folderName}: {exception.Message}");
            }
        }

        return Task.FromResult(new FolderActionResult
        {
            WatchedLocationId = plan.WatchedLocationId,
            ScopeName = plan.ScopeName,
            ProjectDirectory = plan.ProjectDirectory,
            FoldersCreated = created,
            FoldersAlreadyPresent = plan.FoldersAlreadyPresent,
            Errors = errors,
            Source = plan.Source
        });
    }
}
