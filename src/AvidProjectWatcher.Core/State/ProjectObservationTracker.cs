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
