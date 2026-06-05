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

namespace AvidProjectWatcher.Core.State;

public sealed record WatcherState
{
    public Guid InstanceId { get; init; } = Guid.NewGuid();

    public IReadOnlyList<ScopeRuntimeState> Scopes { get; init; } = [];
}

public sealed record ScopeRuntimeState
{
    public Guid ScopeId { get; init; }

    public DateTimeOffset ActivatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string> ObservedProjectDirectories { get; init; } = [];
}
