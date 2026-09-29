using Microsoft.Win32;
using ThemeScheduler.Ui;

namespace ThemeScheduler;

internal sealed class SettingsForm : Form
{
    private const int ContentWidth = 420;

    private readonly TimeBox _lightBox;
    private readonly TimeBox _darkBox;
    private readonly DayTimeline _timeline;
    private readonly Label _summary;
    private readonly ToggleSwitch _appsToggle;
    private readonly ToggleSwitch _systemToggle;
    private readonly ToggleSwitch _enabledToggle;
    private readonly ToggleSwitch _autoStartToggle;
    private readonly Label _error;
    private readonly Icon _formIcon;
    private readonly Action<AppSettings, bool> _onSave;
    private readonly List<Label> _subtleLabels = new();
    private Palette _palette = Palette.Current;

    public SettingsForm(AppSettings current, bool autoStart, Action<AppSettings, bool> onSave)
    {
        _onSave = onSave;

        // Children must be added while layout is suspended so WinForms scales them for the monitor DPI.
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Theme Scheduler";
        Font = UiFonts.Body;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(28, 20, 28, 24);
        _formIcon = TrayIcons.Create(ThemeMode.Dark, paused: false);
        Icon = _formIcon;

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth));

        void Add(Control c)
        {
            if (c is Card) c.Dock = DockStyle.Fill;
            root.RowCount++;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(c, 0, root.RowCount - 1);
        }

        Add(new Label { Text = "Theme Scheduler", Font = UiFonts.Title, AutoSize = true, Margin = new Padding(0, 0, 0, 2) });
        Add(Subtle(new Label
        {
            Text = "Switches between light and dark mode on a schedule.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 22),
        }));

        // Schedule
        Add(SectionLabel("Schedule"));
        _lightBox = new TimeBox { Value = current.LightStart, Font = UiFonts.Time };
        _darkBox = new TimeBox { Value = current.DarkStart, Font = UiFonts.Time };
        _timeline = new DayTimeline { Font = UiFonts.Caption, Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0) };
        _summary = Subtle(new Label { AutoSize = true, Margin = new Padding(0, 4, 0, 12) });

        var schedule = new Card();
        schedule.AddRow(SettingRow("Light mode starts", null, _lightBox));
        schedule.AddRow(new Divider());
        schedule.AddRow(SettingRow("Dark mode starts", null, _darkBox));
        schedule.AddRow(_timeline);
        schedule.AddRow(_summary);
        Add(schedule);

        // Options
        var options = new Card();
        _appsToggle = new ToggleSwitch { Checked = current.ApplyToApps };
        _systemToggle = new ToggleSwitch { Checked = current.ApplyToSystem };
        _enabledToggle = new ToggleSwitch { Checked = current.Enabled };
        _autoStartToggle = new ToggleSwitch { Checked = autoStart };
        options.AddRow(SettingRow("Apps", "Explorer, Settings and most apps", _appsToggle));
        options.AddRow(new Divider());
        options.AddRow(SettingRow("Windows", "Taskbar and Start menu", _systemToggle));
        options.AddRow(new Divider());
        options.AddRow(SettingRow("Follow the schedule", "Turn off to keep the current theme", _enabledToggle));
        options.AddRow(new Divider());
        options.AddRow(SettingRow("Start with Windows", "Runs quietly in the notification area", _autoStartToggle));

        var optionsLabel = SectionLabel("Switch");
        optionsLabel.Margin = new Padding(2, 22, 0, 8);
        Add(optionsLabel);
        Add(options);

        _error = new Label { AutoSize = true, Visible = false, Margin = new Padding(2, 12, 0, 0) };
        Add(_error);

        // Buttons
        var save = new FlatButton { Text = "Save", Primary = true, Margin = new Padding(8, 0, 0, 0) };
        var cancel = new FlatButton { Text = "Cancel", Margin = Padding.Empty };
        save.Click += (_, _) => Save();
        cancel.Click += (_, _) => Close();
        AcceptButton = save;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 22, 0, 0),
            WrapContents = false,
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        Add(buttons);

        Controls.Add(root);
        ActiveControl = save;
        ResumeLayout(false);
        PerformLayout();

        _lightBox.ValueChanged += (_, _) => UpdateSchedulePreview();
        _darkBox.ValueChanged += (_, _) => UpdateSchedulePreview();
        UpdateSchedulePreview();

        ApplyTheme(_palette);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>Re-themes the window, e.g. when the scheduler flips the Windows theme while it's open.</summary>
    public void ApplyTheme(Palette palette)
    {
        _palette = palette;
        BackColor = palette.Window;
        ForeColor = palette.Text;
        ApplyRecursive(this, palette, insideCard: false);
        foreach (var label in _subtleLabels)
            label.ForeColor = palette.SubtleText;
        _error.ForeColor = palette.Error;
        if (IsHandleCreated)
            Dwm.StyleTitleBar(Handle, palette);
        Invalidate(invalidateChildren: true);
    }

    private static void ApplyRecursive(Control parent, Palette palette, bool insideCard)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is IThemed themed)
                themed.ApplyPalette(palette);
            if (child is not Card and not Divider)
                child.BackColor = insideCard ? palette.Card : palette.Window;
            if (child is Label)
                child.ForeColor = palette.Text;
            ApplyRecursive(child, palette, insideCard || child is Card);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Dwm.StyleTitleBar(Handle, _palette);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;
        if (IsHandleCreated && !IsDisposed)
            BeginInvoke(() => ApplyTheme(Palette.Current));
    }

    private void UpdateSchedulePreview()
    {
        var light = _lightBox.Value;
        var dark = _darkBox.Value;
        _timeline.SetTimes(light, dark);

        if (light == dark)
        {
            _summary.Text = "Pick two different times.";
            return;
        }
        var now = Schedule.DesiredAt(TimeOnly.FromDateTime(DateTime.Now), light, dark);
        var next = now == ThemeMode.Light ? "dark" : "light";
        _summary.Text = $"{now} mode now · {next} from {Schedule.NextSwitch(now, light, dark):t}";
    }

    private void Save()
    {
        var light = _lightBox.Value;
        var dark = _darkBox.Value;

        string? problem =
            light == dark ? "Light and dark mode need different start times." :
            !_appsToggle.Checked && !_systemToggle.Checked ? "Turn on at least one of Apps or Windows." :
            null;

        if (problem != null)
        {
            _error.Text = problem;
            _error.Visible = true;
            return;
        }

        var settings = new AppSettings
        {
            LightStart = light,
            DarkStart = dark,
            ApplyToApps = _appsToggle.Checked,
            ApplyToSystem = _systemToggle.Checked,
            Enabled = _enabledToggle.Checked,
        };
        _onSave(settings, _autoStartToggle.Checked);
        Close();
    }

    // ---- Layout helpers ----

    private Label Subtle(Label label)
    {
        _subtleLabels.Add(label);
        return label;
    }

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        Font = UiFonts.Section,
        AutoSize = true,
        Margin = new Padding(2, 0, 0, 8),
    };

    /// <summary>A row with a title (and optional description) on the left and a control on the right.</summary>
    private Control SettingRow(string title, string? description, Control trailing)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = new Padding(0, 12, 0, 12),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var text = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty,
        };
        var titleLabel = new Label { Text = title, AutoSize = true, Margin = Padding.Empty };
        text.Controls.Add(titleLabel);
        Label? descriptionLabel = null;
        if (description != null)
        {
            descriptionLabel = Subtle(new Label
            {
                Text = description,
                Font = UiFonts.Caption,
                AutoSize = true,
                Margin = new Padding(0, 1, 0, 0),
            });
            text.Controls.Add(descriptionLabel);
        }

        trailing.Anchor = AnchorStyles.Right;
        trailing.Margin = new Padding(16, 0, 0, 0);
        trailing.AccessibleName = title;

        // Clicking a toggle's label flips it, like the Windows Settings app.
        if (trailing is ToggleSwitch toggle)
        {
            foreach (var clickable in new Control?[] { row, text, titleLabel, descriptionLabel })
                if (clickable != null)
                    clickable.Click += (_, _) => toggle.Toggle();
        }

        row.Controls.Add(text, 0, 0);
        row.Controls.Add(trailing, 1, 0);
        return row;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _formIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
