namespace AvidProjectWatcher.Core.State;

public interface IStateStore
{
    Task<WatcherState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(WatcherState state, CancellationToken cancellationToken = default);
}
