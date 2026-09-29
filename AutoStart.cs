using Microsoft.Win32;

namespace ThemeScheduler;

/// <summary>Registers the app to launch at Windows sign-in via the per-user Run key (no admin rights needed).</summary>
internal static class AutoStart
{
    public const string StartupArgument = "--startup";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ThemeScheduler";

    private static string Command => $"\"{Environment.ProcessPath}\" {StartupArgument}";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>If the exe was moved since autostart was enabled, point the entry at the new location.</summary>
    public static void RefreshPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string existing && existing != Command)
            key.SetValue(ValueName, Command, RegistryValueKind.String);
    }
}
