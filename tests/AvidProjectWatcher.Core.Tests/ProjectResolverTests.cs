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
using AvidProjectWatcher.Core.Projects;
using Xunit;

namespace AvidProjectWatcher.Core.Tests;

public sealed class ProjectResolverTests
{
    private readonly ProjectResolver resolver = new(new ScopeResolver(), new ExclusionMatcher());

    [Fact]
    public void Resolve_UsesContainingFolderAsProjectDirectory()
    {
        using var workspace = TemporaryWorkspace.Create();
        var projectDirectory = workspace.CreateDirectory("Projects", "2026", "Episode 01");
        var avpPath = workspace.CreateFile(["Projects", "2026", "Episode 01", "Episode 01.avp"]);
        var config = CreateConfig(workspace.GetPath("Projects"));

        var candidate = Assert.IsType<ProjectCandidate>(resolver.Resolve(config, avpPath));

        Assert.Equal(projectDirectory, candidate.ProjectDirectory);
    }

    [Fact]
    public void Resolve_AppliesPathBasedExclusions()
    {
        using var workspace = TemporaryWorkspace.Create();
        var excluded = workspace.CreateDirectory("Projects", "Archive");
        var avpPath = workspace.CreateFile(["Projects", "Archive", "Old", "Old.avp"]);
        var scope = CreateScope(workspace.GetPath("Projects")) with
        {
            ExcludedPaths = [new ExcludedPath(excluded)]
        };

        var candidate = Assert.IsType<ProjectCandidate>(resolver.Resolve(new WatcherConfig { WatchedLocations = [scope] }, avpPath));

        Assert.True(candidate.IsExcluded);
    }

    [Fact]
    public void Resolve_AppliesRelativeExclusionsUnderScopeRoot()
    {
        using var workspace = TemporaryWorkspace.Create();
        var avpPath = workspace.CreateFile(["Projects", "Archive", "Old", "Old.avp"]);
        var scope = CreateScope(workspace.GetPath("Projects")) with
        {
            ExcludedPaths = [new ExcludedPath("Archive")]
        };

        var candidate = Assert.IsType<ProjectCandidate>(resolver.Resolve(new WatcherConfig { WatchedLocations = [scope] }, avpPath));

        Assert.True(candidate.IsExcluded);
    }

    [Fact]
    public void Resolve_ChoosesMostSpecificOverlappingScope()
    {
        using var workspace = TemporaryWorkspace.Create();
        var root = workspace.CreateDirectory("Projects");
        var nested = workspace.CreateDirectory("Projects", "Special");
        var avpPath = workspace.CreateFile(["Projects", "Special", "Show", "Show.avp"]);
        var broadScope = CreateScope(root) with { Name = "Broad" };
        var specificScope = CreateScope(nested) with { Name = "Specific" };

        var candidate = Assert.IsType<ProjectCandidate>(resolver.Resolve(new WatcherConfig { WatchedLocations = [broadScope, specificScope] }, avpPath));

        Assert.Equal(specificScope.Id, candidate.WatchedLocationId);
    }

    private static WatcherConfig CreateConfig(string rootPath)
    {
        return new WatcherConfig { WatchedLocations = [CreateScope(rootPath)] };
    }

    private static WatchedLocation CreateScope(string rootPath)
    {
        return new WatchedLocation
        {
            Name = "Projects",
            RootPath = rootPath,
            FolderTemplate = [new FolderTemplateEntry("FOOTAGE")]
        };
    }
}
