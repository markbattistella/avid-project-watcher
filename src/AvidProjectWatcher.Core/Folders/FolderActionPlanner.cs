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
using AvidProjectWatcher.Core.Templates;

namespace AvidProjectWatcher.Core.Folders;

public sealed class FolderActionPlanner
{
    public FolderActionPlan CreatePlan(
        WatchedLocation scope,
        string projectDirectory,
        FolderActionSource source,
        string? skippedReason = null)
    {
        if (!string.IsNullOrWhiteSpace(skippedReason))
        {
            return new FolderActionPlan
            {
                WatchedLocationId = scope.Id,
                ScopeName = scope.Name,
                ProjectDirectory = projectDirectory,
                SkippedReason = skippedReason,
                Source = source
            };
        }

        var validation = FolderTemplateValidator.ValidateFlatTemplate(scope.FolderTemplate);
        if (!validation.IsValid)
        {
            return new FolderActionPlan
            {
                WatchedLocationId = scope.Id,
                ScopeName = scope.Name,
                ProjectDirectory = projectDirectory,
                SkippedReason = string.Join(" ", validation.Errors),
                Source = source
            };
        }

        var toCreate = new List<string>();
        var alreadyPresent = new List<string>();

        foreach (var entry in scope.FolderTemplate)
        {
            var folderName = entry.RelativePath.Trim();
            var folderPath = Path.Combine(projectDirectory, folderName);

            if (Directory.Exists(folderPath))
            {
                alreadyPresent.Add(folderName);
            }
            else
            {
                toCreate.Add(folderName);
            }
        }

        return new FolderActionPlan
        {
            WatchedLocationId = scope.Id,
            ScopeName = scope.Name,
            ProjectDirectory = projectDirectory,
            FoldersToCreate = toCreate,
            FoldersAlreadyPresent = alreadyPresent,
            Source = source
        };
    }
}
