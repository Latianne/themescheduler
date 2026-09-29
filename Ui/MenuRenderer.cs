namespace ThemeScheduler.Ui;

/// <summary>Flat, palette-aware rendering for the tray menu.</summary>
internal sealed class MenuRenderer : ToolStripRenderer
{
    public Palette Palette { get; set; } = Palette.Current;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) =>
        e.Graphics.Clear(Palette.Card);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // Windows 11 draws its own border around rounded popups.
        if (Dwm.IsWindows11) return;
        using var pen = new Pen(Palette.CardBorder);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        float s = (e.ToolStrip?.DeviceDpi ?? 96) / 96f;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r = new RectangleF(4 * s, 1, e.Item.Width - 8 * s, e.Item.Height - 2);
        Draw.FillRounded(e.Graphics, Palette.ControlHover, r, 4 * s);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Palette.Text : Palette.SubtleText;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        float s = (e.ToolStrip?.DeviceDpi ?? 96) / 96f;
        int y = e.Item.Height / 2;
        using var pen = new Pen(Palette.Divider);
        e.Graphics.DrawLine(pen, 12 * s, y, e.Item.Width - 12 * s, y);
    }
}
