namespace AvidProjectWatcher.Core.Tests;

internal sealed class TemporaryWorkspace : IDisposable
{
    private TemporaryWorkspace(string rootPath)
    {
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public static TemporaryWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "AvidProjectWatcher.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TemporaryWorkspace(root);
    }

    public string GetPath(params string[] segments)
    {
        return segments.Length == 0 ? RootPath : Path.Combine([RootPath, .. segments]);
    }

    public string CreateDirectory(params string[] segments)
    {
        var path = GetPath(segments);
        Directory.CreateDirectory(path);
        return path;
    }

    public string CreateFile(string[] segments)
    {
        var path = GetPath(segments);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, string.Empty);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
