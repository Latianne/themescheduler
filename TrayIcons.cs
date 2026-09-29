using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ThemeScheduler;

/// <summary>Draws the sun / moon tray icons at runtime so the project needs no image assets.</summary>
internal static class TrayIcons
{
    private static readonly Color SunColor = Color.FromArgb(255, 190, 0);
    private static readonly Color MoonColor = Color.FromArgb(140, 160, 255);
    private static readonly Color PausedColor = Color.FromArgb(150, 150, 150);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(ThemeMode mode, bool paused)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            if (mode == ThemeMode.Light)
                DrawSun(g, paused ? PausedColor : SunColor);
            else
                DrawMoon(g, paused ? PausedColor : MoonColor);
        }

        IntPtr handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void DrawSun(Graphics g, Color color)
    {
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        const float c = 16f;
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            g.DrawLine(pen,
                c + (float)(Math.Cos(a) * 11), c + (float)(Math.Sin(a) * 11),
                c + (float)(Math.Cos(a) * 14.5), c + (float)(Math.Sin(a) * 14.5));
        }
        g.FillEllipse(brush, 9, 9, 14, 14);
    }

    private static void DrawMoon(Graphics g, Color color)
    {
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 3, 3, 26, 26);

        // Cut out an offset circle to leave a crescent.
        g.CompositingMode = CompositingMode.SourceCopy;
        using var eraser = new SolidBrush(Color.Transparent);
        g.FillEllipse(eraser, 11, -2, 23, 23);
    }
}
