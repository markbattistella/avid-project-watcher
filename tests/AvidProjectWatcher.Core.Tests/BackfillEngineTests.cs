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

using AvidProjectWatcher.Core.Backfill;
using AvidProjectWatcher.Core.Folders;
using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Projects;
using Xunit;

namespace AvidProjectWatcher.Core.Tests;

public sealed class BackfillEngineTests
{
    [Fact]
    public async Task DryRunAsync_ReportsMissingFoldersWithoutCreatingThem()
    {
        using var workspace = TemporaryWorkspace.Create();
        var projectDirectory = workspace.CreateDirectory("Projects", "2026", "Episode 01");
        workspace.CreateFile(["Projects", "2026", "Episode 01", "Episode 01.avp"]);

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

        var config = new WatcherConfig { WatchedLocations = [scope] };
        var engine = CreateEngine();

        var report = await engine.DryRunAsync(config, new BackfillRequest());

        Assert.Equal(1, report.AffectedProjectCount);
        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "FOOTAGE")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory, "SFX")));
    }

    [Fact]
    public async Task CommitAsync_AppliesDryRunPlan()
    {
        using var workspace = TemporaryWorkspace.Create();
        var projectDirectory = workspace.CreateDirectory("Projects", "Show");
        workspace.CreateFile(["Projects", "Show", "Show.avp"]);

        var scope = new WatchedLocation
        {
            Name = "Projects",
            RootPath = workspace.GetPath("Projects"),
            FolderTemplate = [new FolderTemplateEntry("GFX")]
        };

        var config = new WatcherConfig { WatchedLocations = [scope] };
        var engine = CreateEngine();
        var report = await engine.DryRunAsync(config, new BackfillRequest());

        await engine.CommitAsync(report);

        Assert.True(Directory.Exists(Path.Combine(projectDirectory, "GFX")));
    }

    [Fact]
    public async Task DryRunAsync_SelectedBroadScopeIgnoresProjectsOwnedByUnselectedSpecificScope()
    {
        using var workspace = TemporaryWorkspace.Create();
        workspace.CreateFile(["Projects", "Special", "Show", "Show.avp"]);

        var broadScope = new WatchedLocation
        {
            Name = "Broad",
            RootPath = workspace.GetPath("Projects"),
            FolderTemplate = [new FolderTemplateEntry("FOOTAGE")]
        };
        var specificScope = new WatchedLocation
        {
            Name = "Specific",
            RootPath = workspace.GetPath("Projects", "Special"),
            FolderTemplate = [new FolderTemplateEntry("GFX")]
        };

        var config = new WatcherConfig { WatchedLocations = [broadScope, specificScope] };
        var engine = CreateEngine();

        var report = await engine.DryRunAsync(config, new BackfillRequest { ScopeIds = [broadScope.Id] });

        Assert.Equal(0, report.AffectedProjectCount);
    }

    private static BackfillEngine CreateEngine()
    {
        var resolver = new ProjectResolver(new ScopeResolver(), new ExclusionMatcher());
        return new BackfillEngine(
            new ProjectScanner(resolver),
            new FolderActionPlanner(),
            new FolderCreator());
    }
}
