using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ThemeScheduler.Ui;

/// <summary>Windows 11-style colors for the current light/dark mode, using the user's accent color.</summary>
internal sealed class Palette
{
    public required bool IsDark { get; init; }
    public required Color Window { get; init; }
    public required Color Card { get; init; }
    public required Color CardBorder { get; init; }
    public required Color Divider { get; init; }
    public required Color Text { get; init; }
    public required Color SubtleText { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentHover { get; init; }
    public required Color OnAccent { get; init; }
    public required Color ControlFill { get; init; }
    public required Color ControlHover { get; init; }
    public required Color ControlPressed { get; init; }
    public required Color ControlBorder { get; init; }
    public required Color ToggleOff { get; init; }
    public required Color LightBand { get; init; }
    public required Color DarkBand { get; init; }
    public required Color Error { get; init; }

    public static Palette Current => For(ThemeManager.GetCurrent() ?? ThemeMode.Light);

    public static Palette For(ThemeMode mode)
    {
        Color accent = ReadAccent();
        if (mode == ThemeMode.Dark)
        {
            accent = Blend(accent, Color.White, 0.35f);
            return new Palette
            {
                IsDark = true,
                Window = Rgb(0x202020),
                Card = Rgb(0x2B2B2B),
                CardBorder = Rgb(0x383838),
                Divider = Rgb(0x383838),
                Text = Rgb(0xFFFFFF),
                SubtleText = Rgb(0xA8A8A8),
                Accent = accent,
                AccentHover = Blend(accent, Rgb(0x202020), 0.1f),
                OnAccent = ContrastText(accent),
                ControlFill = Rgb(0x353535),
                ControlHover = Rgb(0x3C3C3C),
                ControlPressed = Rgb(0x303030),
                ControlBorder = Rgb(0x454545),
                ToggleOff = Rgb(0xA8A8A8),
                LightBand = Rgb(0x6B6146),
                DarkBand = Rgb(0x3A4150),
                Error = Rgb(0xFF99A4),
            };
        }

        return new Palette
        {
            IsDark = false,
            Window = Rgb(0xF3F3F3),
            Card = Rgb(0xFFFFFF),
            CardBorder = Rgb(0xE5E5E5),
            Divider = Rgb(0xEAEAEA),
            Text = Rgb(0x1B1B1B),
            SubtleText = Rgb(0x5F5F5F),
            Accent = accent,
            AccentHover = Blend(accent, Color.White, 0.1f),
            OnAccent = ContrastText(accent),
            ControlFill = Rgb(0xFBFBFB),
            ControlHover = Rgb(0xF5F5F5),
            ControlPressed = Rgb(0xEFEFEF),
            ControlBorder = Rgb(0xD9D9D9),
            ToggleOff = Rgb(0x8A8A8A),
            LightBand = Rgb(0xF4E3B5),
            DarkBand = Rgb(0xC9CFDB),
            Error = Rgb(0xC42B1C),
        };
    }

    private static Color ReadAccent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        if (key?.GetValue("AccentColor") is int abgr)
            return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
        return Rgb(0x005FB8);
    }

    /// <summary>Black or white, whichever has the higher WCAG contrast against the given color.</summary>
    private static Color ContrastText(Color c)
    {
        static double Channel(int v)
        {
            double x = v / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }
        double luminance = 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        double againstWhite = 1.05 / (luminance + 0.05);
        double againstBlack = (luminance + 0.05) / 0.05;
        return againstWhite >= againstBlack ? Color.White : Color.Black;
    }

    private static Color Rgb(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    public static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
}

internal interface IThemed
{
    void ApplyPalette(Palette palette);
}

internal static class UiFonts
{
    private static readonly HashSet<string> Installed = LoadInstalled();

    private static readonly string TextFamily = Pick("Segoe UI Variable Text", "Segoe UI");
    private static readonly string SemiboldFamily = Pick("Segoe UI Variable Text Semibold", "Segoe UI Semibold");
    private static readonly string DisplaySemiboldFamily = Pick("Segoe UI Variable Display Semib", "Segoe UI Semibold");

    public static readonly Font Body = new(TextFamily, 10F);
    public static readonly Font Caption = new(TextFamily, 8.75F);
    public static readonly Font Strong = new(SemiboldFamily, 10F);
    public static readonly Font Section = new(SemiboldFamily, 10.5F);
    public static readonly Font Title = new(DisplaySemiboldFamily, 17F);
    public static readonly Font Time = new(TextFamily, 11F);

    private static HashSet<string> LoadInstalled()
    {
        using var fonts = new InstalledFontCollection();
        return fonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Pick(params string[] candidates) =>
        candidates.FirstOrDefault(Installed.Contains) ?? candidates[^1];
}

internal static class Draw
{
    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRounded(Graphics g, Color color, RectangleF r, float radius, float width = 1f)
    {
        using var path = RoundedRect(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }
}

internal static class Dwm
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const int DWMWCP_ROUNDSMALL = 3;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>Makes the title bar match the window (seamless on Windows 11, dark-aware on Windows 10).</summary>
    public static void StyleTitleBar(IntPtr hwnd, Palette palette)
    {
        Set(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, palette.IsDark ? 1 : 0);
        Set(hwnd, DWMWA_CAPTION_COLOR, ColorTranslator.ToWin32(palette.Window));
        Set(hwnd, DWMWA_TEXT_COLOR, ColorTranslator.ToWin32(palette.Text));
    }

    public static void RoundSmall(IntPtr hwnd) => Set(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUNDSMALL);

    private static void Set(IntPtr hwnd, int attribute, int value) =>
        DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
}
