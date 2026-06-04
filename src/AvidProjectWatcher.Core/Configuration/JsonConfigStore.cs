using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Core.Configuration;

public sealed class JsonConfigStore(string configPath) : IConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string ConfigPath { get; } = configPath;

    public async Task<WatcherConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            return WatcherConfig.Empty;
        }

        await using var stream = File.OpenRead(ConfigPath);
        return await JsonSerializer.DeserializeAsync<WatcherConfig>(stream, SerializerOptions, cancellationToken)
            ?? WatcherConfig.Empty;
    }

    public async Task SaveAsync(WatcherConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{ConfigPath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, SerializerOptions, cancellationToken);
        }

        if (File.Exists(ConfigPath))
        {
            File.Replace(tempPath, ConfigPath, null);
        }
        else
        {
            File.Move(tempPath, ConfigPath);
        }
    }
}
