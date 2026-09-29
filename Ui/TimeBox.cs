using System.ComponentModel;
using System.Globalization;

namespace ThemeScheduler.Ui;

/// <summary>
/// Compact time input with hour / minute (/ AM-PM) segments.
/// Click a segment, then type digits, use the arrow keys or the mouse wheel. The chevrons step the selected segment.
/// </summary>
internal sealed class TimeBox : ThemedControl
{
    private enum Segment { Hour, Minute, Period }

    private readonly bool _is12Hour;
    private readonly string _am;
    private readonly string _pm;

    private int _hour;
    private int _minute;
    private Segment _segment = Segment.Hour;
    private string _typed = "";
    private bool _hover;
    private int _hoverChevron; // -1 down, 0 none, 1 up

    private readonly Dictionary<Segment, Rectangle> _segmentRects = new();
    private Rectangle _upRect;
    private Rectangle _downRect;

    public event EventHandler? ValueChanged;

    public TimeBox()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Size = new Size(128, 34);
        AccessibleRole = AccessibleRole.SpinButton;

        var format = CultureInfo.CurrentCulture.DateTimeFormat;
        _is12Hour = !format.ShortTimePattern.Contains('H');
        _am = string.IsNullOrEmpty(format.AMDesignator) ? "AM" : format.AMDesignator;
        _pm = string.IsNullOrEmpty(format.PMDesignator) ? "PM" : format.PMDesignator;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TimeOnly Value
    {
        get => new(_hour, _minute);
        set
        {
            _hour = value.Hour;
            _minute = value.Minute;
            UpdateAccessibleValue();
            Invalidate();
        }
    }

    private string HourText => _is12Hour ? (_hour % 12 == 0 ? 12 : _hour % 12).ToString() : _hour.ToString("D2");
    private string MinuteText => _minute.ToString("D2");
    private string PeriodText => _hour < 12 ? _am : _pm;

    private IEnumerable<Segment> Segments =>
        _is12Hour ? [Segment.Hour, Segment.Minute, Segment.Period] : [Segment.Hour, Segment.Minute];

    private string TextOf(Segment segment) => segment switch
    {
        Segment.Hour => HourText,
        Segment.Minute => MinuteText,
        _ => PeriodText,
    };

    private void LayoutSegments()
    {
        float s = S;
        int x = (int)(8 * s);
        int pad = (int)(2 * s);
        int top = (int)(5 * s);
        int height = Height - 2 * top;
        int colon = Measure(":");

        foreach (var segment in Segments)
        {
            if (segment == Segment.Minute) x += colon;
            if (segment == Segment.Period) x += (int)(4 * s);
            int width = Measure(TextOf(segment)) + 2 * pad;
            _segmentRects[segment] = new Rectangle(x, top, width, height);
            x += width;
        }

        int chevronWidth = (int)(22 * s);
        int cx = Width - chevronWidth - (int)(3 * s);
        int inner = Height - 2 * (int)(3 * s);
        _upRect = new Rectangle(cx, (int)(3 * s), chevronWidth, inner / 2);
        _downRect = new Rectangle(cx, _upRect.Bottom, chevronWidth, inner - inner / 2);
    }

    private int Measure(string text) =>
        TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;

    protected override void OnFontChanged(EventArgs e) { Invalidate(); base.OnFontChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        LayoutSegments();
        var g = Prepare(e);
        float s = S;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = 4 * s;

        Color fill = Focused ? Palette.Card : _hover ? Palette.ControlHover : Palette.ControlFill;
        Draw.FillRounded(g, fill, r, radius);
        Draw.StrokeRounded(g, Palette.ControlBorder, r, radius);

        if (Focused)
        {
            // Accent underline, like a focused Windows 11 text box.
            using var path = Draw.RoundedRect(r, radius);
            var state = g.Save();
            g.SetClip(path);
            using var accent = new SolidBrush(Palette.Accent);
            g.FillRectangle(accent, 0, Height - 2 * s, Width, 2 * s);
            g.Restore(state);
        }

        const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                      TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        foreach (var segment in Segments)
        {
            var rect = _segmentRects[segment];
            bool selected = Focused && segment == _segment;
            if (selected)
                Draw.FillRounded(g, Palette.Accent, rect, 3 * s);
            TextRenderer.DrawText(g, TextOf(segment), Font, rect, selected ? Palette.OnAccent : Palette.Text, flags);

            if (segment == Segment.Hour)
            {
                var colon = new Rectangle(rect.Right, rect.Y, Measure(":"), rect.Height);
                TextRenderer.DrawText(g, ":", Font, colon, Palette.SubtleText, flags);
            }
        }

        DrawChevron(g, _upRect, up: true, _hoverChevron == 1);
        DrawChevron(g, _downRect, up: false, _hoverChevron == -1);
    }

    private void DrawChevron(Graphics g, Rectangle rect, bool up, bool hot)
    {
        float s = S;
        if (hot)
            Draw.FillRounded(g, Palette.ControlPressed, rect, 3 * s);

        float cx = rect.X + rect.Width / 2f;
        float cy = rect.Y + rect.Height / 2f;
        float w = 3.5f * s;
        float h = 2f * s * (up ? -1 : 1);
        using var pen = new Pen(hot ? Palette.Text : Palette.SubtleText, 1.2f * s)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
        };
        g.DrawLines(pen, new[] { new PointF(cx - w, cy - h / 2), new PointF(cx, cy + h / 2), new PointF(cx + w, cy - h / 2) });
    }

    // ---- Input ----

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        _typed = "";
        if (_upRect.Contains(e.Location)) Step(+1);
        else if (_downRect.Contains(e.Location)) Step(-1);
        else
        {
            var hit = Segments.Cast<Segment?>().FirstOrDefault(seg => _segmentRects[seg!.Value].Contains(e.Location))
                      ?? Segments.MinBy(seg => Math.Abs(e.X - (_segmentRects[seg].X + _segmentRects[seg].Width / 2)));
            _segment = hit;
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int chevron = _upRect.Contains(e.Location) ? 1 : _downRect.Contains(e.Location) ? -1 : 0;
        if (chevron != _hoverChevron)
        {
            _hoverChevron = chevron;
            Invalidate();
        }
        Cursor = chevron != 0 ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _hoverChevron = 0;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (Focused)
        {
            Step(e.Delta > 0 ? 1 : -1);
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }
        base.OnMouseWheel(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

    protected override void OnLostFocus(EventArgs e)
    {
        _typed = "";
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up: Step(+1, fine: true); break;
            case Keys.Down: Step(-1, fine: true); break;
            case Keys.Left: MoveSegment(-1); break;
            case Keys.Right: MoveSegment(+1); break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (char.IsDigit(e.KeyChar))
        {
            TypeDigit(e.KeyChar - '0');
            e.Handled = true;
        }
        else if (_is12Hour && char.ToLowerInvariant(e.KeyChar) is 'a' or 'p')
        {
            bool pm = char.ToLowerInvariant(e.KeyChar) == 'p';
            if (pm != _hour >= 12) SetHour((_hour + 12) % 24);
            e.Handled = true;
        }
        base.OnKeyPress(e);
    }

    private void MoveSegment(int direction)
    {
        var list = Segments.ToList();
        int i = Math.Clamp(list.IndexOf(_segment) + direction, 0, list.Count - 1);
        _segment = list[i];
        _typed = "";
        Invalidate();
    }

    /// <summary>Arrow keys move by one; the wheel and chevrons move minutes in 5-minute steps.</summary>
    private void Step(int direction, bool fine = false)
    {
        _typed = "";
        switch (_segment)
        {
            case Segment.Hour:
                SetHour((_hour + direction + 24) % 24);
                break;
            case Segment.Minute:
                int minute = fine
                    ? _minute + direction
                    : direction > 0 ? (_minute / 5 + 1) * 5 : ((_minute + 4) / 5 - 1) * 5;
                SetMinute((minute % 60 + 60) % 60);
                break;
            case Segment.Period:
                SetHour((_hour + 12) % 24);
                break;
        }
    }

    private void TypeDigit(int digit)
    {
        _typed += digit;
        int value = int.Parse(_typed);

        switch (_segment)
        {
            case Segment.Hour when !_is12Hour:
                if (_typed.Length == 1)
                {
                    SetHour(value);
                    if (value > 2) Advance();
                }
                else
                {
                    SetHour(value <= 23 ? value : digit);
                    Advance();
                }
                break;

            case Segment.Hour:
                if (_typed.Length == 1)
                {
                    if (value > 0) SetHour12(value);
                    if (value > 1) Advance();
                }
                else
                {
                    if (value is >= 1 and <= 12) SetHour12(value);
                    else if (digit > 0) SetHour12(digit);
                    Advance();
                }
                break;

            case Segment.Minute:
                SetMinute(value);
                if (_typed.Length == 2 || value > 5)
                {
                    _typed = "";
                    if (_is12Hour) Advance();
                }
                break;

            default:
                _typed = "";
                break;
        }
    }

    private void Advance()
    {
        _typed = "";
        MoveSegment(+1);
    }

    private void SetHour12(int hour12)
    {
        bool pm = _hour >= 12;
        SetHour(hour12 % 12 + (pm ? 12 : 0));
    }

    private void SetHour(int hour)
    {
        if (_hour == hour) { Invalidate(); return; }
        _hour = hour;
        OnValueChanged();
    }

    private void SetMinute(int minute)
    {
        if (_minute == minute) { Invalidate(); return; }
        _minute = minute;
        OnValueChanged();
    }

    private void OnValueChanged()
    {
        UpdateAccessibleValue();
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateAccessibleValue() => AccessibleDescription = Value.ToString("t");
}

/// <summary>A 24-hour bar showing the light and dark periods, with a marker for the current time.</summary>
internal sealed class DayTimeline : ThemedControl
{
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 30_000 };
    private TimeOnly _light;
    private TimeOnly _dark;

    public DayTimeline()
    {
        Size = new Size(300, 42);
        Margin = Padding.Empty;
        _clock.Tick += (_, _) => Invalidate();
        _clock.Start();
    }

    public void SetTimes(TimeOnly light, TimeOnly dark)
    {
        _light = light;
        _dark = dark;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = Prepare(e);
        float s = S;
        float barHeight = 8 * s;
        var bar = new RectangleF(1, 6 * s, Width - 2, barHeight);

        using (var path = Draw.RoundedRect(bar, barHeight / 2))
        {
            var state = g.Save();
            g.SetClip(path);
            using (var dark = new SolidBrush(Palette.DarkBand))
                g.FillRectangle(dark, bar);

            using var light = new SolidBrush(Palette.LightBand);
            float X(TimeOnly t) => bar.X + bar.Width * (float)(t.ToTimeSpan().TotalHours / 24);
            if (_light < _dark)
            {
                g.FillRectangle(light, X(_light), bar.Y, X(_dark) - X(_light), bar.Height);
            }
            else if (_light > _dark)
            {
                g.FillRectangle(light, X(_light), bar.Y, bar.Right - X(_light), bar.Height);
                g.FillRectangle(light, bar.X, bar.Y, X(_dark) - bar.X, bar.Height);
            }
            else
            {
                g.FillRectangle(light, bar);
            }
            g.Restore(state);
        }

        // Current time marker.
        var now = TimeOnly.FromDateTime(DateTime.Now);
        float nx = bar.X + bar.Width * (float)(now.ToTimeSpan().TotalHours / 24);
        float markerWidth = 3 * s;
        Draw.FillRounded(g, Palette.Text, new RectangleF(nx - markerWidth / 2, bar.Y - 4 * s, markerWidth, bar.Height + 8 * s), markerWidth / 2);

        // Hour labels.
        int labelTop = (int)(bar.Bottom + 6 * s);
        var labels = new[] { 0, 6, 12, 18, 24 };
        foreach (int hour in labels)
        {
            string text = hour.ToString();
            int width = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            float x = bar.X + bar.Width * hour / 24f;
            int left = hour == 0 ? (int)bar.X : hour == 24 ? (int)(bar.Right - width) : (int)(x - width / 2f);
            TextRenderer.DrawText(g, text, Font, new Point(left, labelTop), Palette.SubtleText, TextFormatFlags.NoPadding);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _clock.Dispose();
        base.Dispose(disposing);
    }
}
