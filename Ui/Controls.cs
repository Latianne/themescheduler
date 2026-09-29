using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ThemeScheduler.Ui;

/// <summary>Base for owner-drawn controls: double-buffered, DPI-aware helpers, palette storage.</summary>
internal abstract class ThemedControl : Control, IThemed
{
    protected Palette Palette { get; private set; } = Palette.Current;

    protected ThemedControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    protected float S => DeviceDpi / 96f;

    public virtual void ApplyPalette(Palette palette)
    {
        Palette = palette;
        Invalidate();
    }

    protected Graphics Prepare(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        return g;
    }
}

/// <summary>Rounded surface that groups related settings.</summary>
internal sealed class Card : Panel, IThemed
{
    private Palette _palette = Palette.Current;

    public TableLayoutPanel Content { get; }

    public Card()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(18, 6, 18, 6);
        Margin = Padding.Empty;

        Content = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        Content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(Content);
    }

    public void AddRow(Control row)
    {
        row.Dock = DockStyle.Fill;
        Content.RowCount++;
        Content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Content.Controls.Add(row, 0, Content.RowCount - 1);
    }

    public void ApplyPalette(Palette palette)
    {
        _palette = palette;
        BackColor = palette.Window;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(_palette.Window);
        float s = DeviceDpi / 96f;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Draw.FillRounded(g, _palette.Card, r, 8 * s);
        Draw.StrokeRounded(g, _palette.CardBorder, r, 8 * s);
    }
}

internal sealed class Divider : Control, IThemed
{
    public Divider()
    {
        Height = 1;
        Margin = Padding.Empty;
    }

    public void ApplyPalette(Palette palette) => BackColor = palette.Divider;
}

/// <summary>Windows 11-style on/off switch.</summary>
internal sealed class ToggleSwitch : ThemedControl
{
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 15 };
    private bool _checked;
    private float _position;
    private bool _hover;

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Size = new Size(44, 24);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.CheckButton;
        _animation.Tick += (_, _) =>
        {
            float target = _checked ? 1 : 0;
            _position += Math.Sign(target - _position) * 0.2f;
            if (Math.Abs(target - _position) < 0.2f)
            {
                _position = target;
                _animation.Stop();
            }
            Invalidate();
        };
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            if (IsHandleCreated && Visible) _animation.Start();
            else _position = value ? 1 : 0;
            AccessibleDescription = value ? "On" : "Off";
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Toggle() => Checked = !Checked;

    protected override void OnClick(EventArgs e)
    {
        Focus();
        Toggle();
        base.OnClick(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) Toggle();
        base.OnKeyUp(e);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prepare(e);
        float s = S;
        var track = new RectangleF(2 * s, 2 * s, Width - 4 * s - 1, Height - 4 * s - 1);
        float radius = track.Height / 2;

        if (_position > 0.5f)
        {
            Draw.FillRounded(g, _hover ? Palette.AccentHover : Palette.Accent, track, radius);
        }
        else
        {
            Draw.FillRounded(g, _hover ? Palette.ControlHover : Palette.ControlFill, track, radius);
            Draw.StrokeRounded(g, Palette.ToggleOff, track, radius);
        }

        float knob = (_hover ? 14 : 12) * s;
        float travel = track.Width - track.Height;
        float cx = track.X + track.Height / 2 + travel * _position;
        float cy = track.Y + track.Height / 2;
        using (var brush = new SolidBrush(_position > 0.5f ? Palette.OnAccent : Palette.ToggleOff))
            g.FillEllipse(brush, cx - knob / 2, cy - knob / 2, knob, knob);

        if (Focused && ShowFocusCues)
            Draw.StrokeRounded(g, Palette.Text, new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), Height / 2f, 1.5f * s);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animation.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Flat rounded button. Primary buttons use the accent color.</summary>
internal sealed class FlatButton : ThemedControl, IButtonControl
{
    private bool _hover;
    private bool _pressed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; init; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DialogResult DialogResult { get; set; }

    public FlatButton()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Size = new Size(104, 34);
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public void NotifyDefault(bool value) { }

    public void PerformClick()
    {
        if (CanSelect) OnClick(EventArgs.Empty);
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) PerformClick();
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prepare(e);
        float s = S;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = 4 * s;

        Color text;
        if (Primary)
        {
            var fill = _pressed ? Palette.Blend(Palette.Accent, Palette.Window, 0.2f)
                     : _hover ? Palette.AccentHover : Palette.Accent;
            Draw.FillRounded(g, fill, r, radius);
            text = Palette.OnAccent;
        }
        else
        {
            var fill = _pressed ? Palette.ControlPressed : _hover ? Palette.ControlHover : Palette.ControlFill;
            Draw.FillRounded(g, fill, r, radius);
            Draw.StrokeRounded(g, Palette.ControlBorder, r, radius);
            text = Palette.Text;
        }

        if (Focused && ShowFocusCues)
            Draw.StrokeRounded(g, Palette.Text, new RectangleF(1, 1, Width - 3, Height - 3), radius, 1.5f * s);

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
