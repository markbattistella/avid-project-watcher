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
using Xunit;

namespace AvidProjectWatcher.Core.Tests;

public sealed class FolderTemplateValidatorTests
{
    [Fact]
    public void ValidateFlatTemplate_AllowsSimpleFolderNames()
    {
        var result = FolderTemplateValidator.ValidateFlatTemplate([
            new FolderTemplateEntry("FOOTAGE"),
            new FolderTemplateEntry("SFX")
        ]);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("FOOTAGE/RAW")]
    [InlineData("FOOTAGE\\RAW")]
    [InlineData(".")]
    [InlineData("..")]
    public void ValidateFlatTemplate_RejectsNonFlatOrUnsafeValues(string value)
    {
        var result = FolderTemplateValidator.ValidateFlatTemplate([new FolderTemplateEntry(value)]);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateFlatTemplate_RejectsDuplicates()
    {
        var result = FolderTemplateValidator.ValidateFlatTemplate([
            new FolderTemplateEntry("FOOTAGE"),
            new FolderTemplateEntry("footage")
        ]);

        Assert.False(result.IsValid);
    }
}
