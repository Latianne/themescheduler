using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ThemeScheduler;

internal static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsValue = "AppsUseLightTheme";
    private const string SystemValue = "SystemUsesLightTheme";

    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeoutMs, out UIntPtr result);

    /// <summary>The theme currently used by apps (falls back to the shell setting).</summary>
    public static ThemeMode? GetCurrent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        object? value = key?.GetValue(AppsValue) ?? key?.GetValue(SystemValue);
        return value is int i ? (i == 0 ? ThemeMode.Dark : ThemeMode.Light) : null;
    }

    public static void Apply(ThemeMode mode, bool apps, bool system)
    {
        int value = mode == ThemeMode.Light ? 1 : 0;
        bool changed = false;

        using (var key = Registry.CurrentUser.CreateSubKey(PersonalizeKey, writable: true))
        {
            if (apps) changed |= SetIfDifferent(key, AppsValue, value);
            if (system) changed |= SetIfDifferent(key, SystemValue, value);
        }

        if (changed)
        {
            // Tell running windows (taskbar, Explorer, apps) that the color set changed.
            // Done off the UI thread so an unresponsive window can't freeze the tray icon.
            Task.Run(() => SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero,
                "ImmersiveColorSet", SMTO_ABORTIFHUNG, 1000, out _));
        }
    }

    private static bool SetIfDifferent(RegistryKey key, string name, int value)
    {
        if (key.GetValue(name) is int current && current == value)
            return false;
        key.SetValue(name, value, RegistryValueKind.DWord);
        return true;
    }
}
