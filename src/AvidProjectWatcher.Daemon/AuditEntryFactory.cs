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

using AvidProjectWatcher.Core.Audit;
using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Daemon;

public static class AuditEntryFactory
{
    public static AuditLogEntry FromFolderResult(FolderActionResult result)
    {
        return new AuditLogEntry
        {
            EventType = result.Errors.Count > 0 ? AuditEventType.WatcherError : AuditEventType.FoldersCreated,
            ScopeId = result.WatchedLocationId,
            ScopeName = result.ScopeName,
            ProjectPath = result.ProjectDirectory,
            Trigger = result.Source.ToString(),
            FoldersCreated = result.FoldersCreated,
            FoldersAlreadyPresent = result.FoldersAlreadyPresent,
            Message = result.Errors.Count > 0 ? string.Join(" ", result.Errors) : "Folder creation completed.",
            IsError = result.Errors.Count > 0
        };
    }
}
