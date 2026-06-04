namespace AvidProjectWatcher.Core.Configuration;

public static class ConfigDefaults
{
    public static int DefaultApiPort =>
        int.TryParse(Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_API_PORT"), out var port)
            ? port
            : 47821;

    public static string AppDataDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "AvidProjectWatcher");
        }
    }

    public static string DefaultConfigPath =>
        Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_CONFIG")
        ?? Path.Combine(AppDataDirectory, "config.json");

    public static string DefaultStatePath =>
        Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_STATE")
        ?? Path.Combine(AppDataDirectory, "state.json");

    public static string DefaultAuditDatabasePath =>
        Environment.GetEnvironmentVariable("AVID_PROJECT_WATCHER_AUDIT_DB")
        ?? Path.Combine(AppDataDirectory, "audit.sqlite");
}
