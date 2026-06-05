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

namespace AvidProjectWatcher.Core.Models;

public enum FolderActionSource
{
    Live,
    NewReconciliation,
    ManualBackfill
}

public sealed record FolderActionPlan
{
    public Guid WatchedLocationId { get; init; }

    public string ScopeName { get; init; } = string.Empty;

    public string ProjectDirectory { get; init; } = string.Empty;

    public IReadOnlyList<string> FoldersToCreate { get; init; } = [];

    public IReadOnlyList<string> FoldersAlreadyPresent { get; init; } = [];

    public string? SkippedReason { get; init; }

    public FolderActionSource Source { get; init; }

    [JsonIgnore]
    public bool HasWork => FoldersToCreate.Count > 0 && string.IsNullOrWhiteSpace(SkippedReason);
}
