using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Core.Projects;

public sealed record ScopeOverlap(WatchedLocation First, WatchedLocation Second);
