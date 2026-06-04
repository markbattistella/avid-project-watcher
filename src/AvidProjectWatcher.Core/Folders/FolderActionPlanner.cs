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
