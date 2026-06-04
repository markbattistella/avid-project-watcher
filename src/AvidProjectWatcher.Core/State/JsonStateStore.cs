namespace AvidProjectWatcher.Core.State;

public sealed class JsonStateStore(string statePath) : IStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<WatcherState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(statePath))
        {
            return new WatcherState();
        }

        await using var stream = File.OpenRead(statePath);
        return await JsonSerializer.DeserializeAsync<WatcherState>(stream, SerializerOptions, cancellationToken)
            ?? new WatcherState();
    }

    public async Task SaveAsync(WatcherState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(statePath);
        await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
    }
}
