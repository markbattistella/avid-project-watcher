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
        Assert.True(result.Succeeded);
        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "SFX")));
    }

    [Fact]
    public async Task ApplyAsync_SkippedPlanReportsFailure()
    {
        using var workspace = TemporaryWorkspace.Create();
        var projectDirectory = workspace.CreateDirectory("Projects", "Episode 01");
        var creator = new FolderCreator();

        var result = await creator.ApplyAsync(new FolderActionPlan
        {
            ScopeName = "Projects",
            ProjectDirectory = projectDirectory,
            SkippedReason = "Invalid folder template.",
            Source = FolderActionSource.Live
        });

        Assert.False(result.Succeeded);
        Assert.Contains("Invalid folder template.", result.Errors);
    }
}
