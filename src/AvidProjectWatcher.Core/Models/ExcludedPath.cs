namespace AvidProjectWatcher.Core.Models;

public sealed record ExcludedPath
{
    public ExcludedPath()
    {
    }

    public ExcludedPath(string path)
    {
        Path = path;
    }

    public string Path { get; init; } = string.Empty;
}
