using AvidProjectWatcher.Core.Folders;
using AvidProjectWatcher.Core.Models;
using Xunit;

namespace AvidProjectWatcher.Core.Tests;

public sealed class FolderCreatorTests
{
    [Fact]
    public async Task ApplyAsync_CreatesOnlyMissingFolders()
    {
        using var workspace = TemporaryWorkspace.Create();
        var projectDirectory = workspace.CreateDirectory("Projects", "Episode 01");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "FOOTAGE"));

        var scope = new WatchedLocation
        {
            Name = "Projects",
            RootPath = workspace.GetPath("Projects"),
            FolderTemplate =
            [
                new FolderTemplateEntry("FOOTAGE"),
                new FolderTemplateEntry("SFX")
            ]
        };

        var planner = new FolderActionPlanner();
        var creator = new FolderCreator();
        var plan = planner.CreatePlan(scope, projectDirectory, FolderActionSource.Live);

        var result = await creator.ApplyAsync(plan);

        Assert.Contains("SFX", result.FoldersCreated);
        Assert.Contains("FOOTAGE", result.FoldersAlreadyPresent);
        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "SFX")));
    }
}
