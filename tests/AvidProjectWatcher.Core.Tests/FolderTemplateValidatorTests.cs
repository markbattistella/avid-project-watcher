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
