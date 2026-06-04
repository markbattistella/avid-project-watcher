using AvidProjectWatcher.Core.Models;
using AvidProjectWatcher.Core.Paths;

namespace AvidProjectWatcher.Core.State;

public sealed class ProjectObservationTracker(IStateStore stateStore)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<WatcherState> EnsureScopesAsync(WatcherConfig config, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var state = await stateStore.LoadAsync(cancellationToken);
            var existing = state.Scopes.ToDictionary(scope => scope.ScopeId);
            var scopes = new List<ScopeRuntimeState>();

            foreach (var watchedLocation in config.WatchedLocations)
            {
                if (existing.TryGetValue(watchedLocation.Id, out var runtimeState))
                {
                    scopes.Add(runtimeState);
                }
                else
                {
                    scopes.Add(new ScopeRuntimeState { ScopeId = watchedLocation.Id });
                }
            }

            var nextState = state with { Scopes = scopes };
            await stateStore.SaveAsync(nextState, cancellationToken);
            return nextState;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> HasObservedAsync(Guid scopeId, string projectDirectory, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var normalized = PathUtility.NormalizeFullPath(projectDirectory);
            var state = await stateStore.LoadAsync(cancellationToken);
            var scope = state.Scopes.FirstOrDefault(scope => scope.ScopeId == scopeId);
            return scope?.ObservedProjectDirectories.Contains(normalized, PathUtility.PathComparer) ?? false;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task MarkObservedAsync(Guid scopeId, string projectDirectory, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var normalized = PathUtility.NormalizeFullPath(projectDirectory);
            var state = await stateStore.LoadAsync(cancellationToken);
            var scopes = state.Scopes.ToList();
            var index = scopes.FindIndex(scope => scope.ScopeId == scopeId);

            if (index < 0)
            {
                scopes.Add(new ScopeRuntimeState
                {
                    ScopeId = scopeId,
                    ObservedProjectDirectories = [normalized]
                });
            }
            else
            {
                var scope = scopes[index];
                if (!scope.ObservedProjectDirectories.Contains(normalized, PathUtility.PathComparer))
                {
                    scopes[index] = scope with
                    {
                        ObservedProjectDirectories = [.. scope.ObservedProjectDirectories, normalized]
                    };
                }
            }

            await stateStore.SaveAsync(state with { Scopes = scopes }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<DateTimeOffset> GetActivationTimeAsync(Guid scopeId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var state = await stateStore.LoadAsync(cancellationToken);
            return state.Scopes.FirstOrDefault(scope => scope.ScopeId == scopeId)?.ActivatedAtUtc
                ?? DateTimeOffset.UtcNow;
        }
        finally
        {
            gate.Release();
        }
    }
}
