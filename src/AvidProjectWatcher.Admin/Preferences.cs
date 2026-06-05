using System.Text.Json;
using AvidProjectWatcher.Core.Configuration;

namespace AvidProjectWatcher.Admin;

public sealed record Preferences
{
    public string DaemonBaseUrl { get; init; } = $"http://localhost:{ConfigDefaults.DefaultApiPort}";
}

public sealed class PreferencesStore
{
    private static readonly string PrefsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AvidProjectWatcher",
        "preferences.json");

    public Preferences Load()
    {
        try
        {
            if (!File.Exists(PrefsPath)) return new Preferences();
            var json = File.ReadAllText(PrefsPath);
            return JsonSerializer.Deserialize<Preferences>(json) ?? new Preferences();
        }
        catch
        {
            return new Preferences();
        }
    }

    public void Save(Preferences prefs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefsPath)!);
        File.WriteAllText(PrefsPath, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
    }
}
