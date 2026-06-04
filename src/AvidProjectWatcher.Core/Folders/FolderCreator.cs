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
