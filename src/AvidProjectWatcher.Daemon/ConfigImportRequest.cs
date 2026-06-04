using AvidProjectWatcher.Core.Models;

namespace AvidProjectWatcher.Daemon;

public sealed record ConfigImportRequest(WatcherConfig Config);
