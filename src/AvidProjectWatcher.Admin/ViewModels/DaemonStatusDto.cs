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

using AvidProjectWatcher.Core.Discovery;
using AvidProjectWatcher.Core.Watching;

namespace AvidProjectWatcher.Admin.ViewModels;

public sealed record DaemonStatusDto
{
    public Guid InstanceId { get; init; }

    public string MachineName { get; init; } = string.Empty;

    public string ConfigPath { get; init; } = string.Empty;

    public string StatePath { get; init; } = string.Empty;

    public string AuditDatabasePath { get; init; } = string.Empty;

    public DateTimeOffset? LastConfigReloadUtc { get; init; }

    public IReadOnlyList<ScopeWatcherStatus> Watchers { get; init; } = [];

    public IReadOnlyList<DuplicateWatcherWarning> DuplicateWarnings { get; init; } = [];
}
