namespace AvidProjectWatcher.Core.Discovery;

public sealed record DuplicateWatcherWarning(
    Guid RemoteInstanceId,
    string RemoteMachineName,
    string LocalScopeName,
    string RemoteScopeName,
    string LocalRootPath,
    string RemoteRootPath);
