namespace AvidProjectWatcher.Core.Models;

public sealed record FolderTemplateEntry
{
    public FolderTemplateEntry()
    {
    }

    public FolderTemplateEntry(string relativePath)
    {
        RelativePath = relativePath;
    }

    public string RelativePath { get; init; } = string.Empty;
}
