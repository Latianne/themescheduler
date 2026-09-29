using System.Text.Json;

namespace ThemeScheduler;

internal sealed class AppSettings
{
    public TimeOnly LightStart { get; set; } = new(7, 0);
    public TimeOnly DarkStart { get; set; } = new(19, 0);

    /// <summary>Switch app windows (Explorer, Settings, most modern apps).</summary>
    public bool ApplyToApps { get; set; } = true;

    /// <summary>Switch the Windows shell (taskbar, Start menu, Action Center).</summary>
    public bool ApplyToSystem { get; set; } = true;

    public bool Enabled { get; set; } = true;

    private static readonly string FolderPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThemeScheduler");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static bool Exists => File.Exists(FilePath);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable file: fall back to defaults.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
