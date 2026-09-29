using Microsoft.Win32;
using ThemeScheduler.Ui;

namespace ThemeScheduler;

/// <summary>Owns the tray icon, the schedule timer and the settings window. Lives for the whole app lifetime.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Control _invoker;
    private readonly RegisteredWaitHandle _showSettingsWait;

    private AppSettings _settings;
    private ThemeMode? _lastScheduled;
    private SettingsForm? _settingsForm;
    private Icon? _icon;
    private (ThemeMode Mode, bool Paused)? _iconState;
    private bool _reportedApplyError;

    public TrayContext(EventWaitHandle showSettingsEvent, bool openSettings)
    {
        bool firstRun = !AppSettings.Exists;
        _settings = AppSettings.Load();
        if (firstRun)
        {
            _settings.Save();
            AutoStart.Set(true);
        }
        else
        {
            AutoStart.RefreshPath();
        }

        // Hidden control used to marshal callbacks from other threads onto the UI thread.
        _invoker = new Control();
        _ = _invoker.Handle;

        _statusItem = new ToolStripMenuItem { Enabled = false };
        _pauseItem = new ToolStripMenuItem("Pause schedule", null, (_, _) => TogglePause());

        var renderer = new MenuRenderer();
        var menu = new ContextMenuStrip
        {
            Renderer = renderer,
            Font = UiFonts.Body,
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Padding = new Padding(0, 4, 0, 4),
        };
        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Switch to light now", null, (_, _) => ApplyManually(ThemeMode.Light)),
            new ToolStripMenuItem("Switch to dark now", null, (_, _) => ApplyManually(ThemeMode.Dark)),
            _pauseItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Settings", null, (_, _) => ShowSettings()),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()),
        });
        foreach (ToolStripItem item in menu.Items)
            if (item is ToolStripMenuItem)
                item.Padding = new Padding(6, 5, 18, 5);
        menu.HandleCreated += (_, _) => Dwm.RoundSmall(menu.Handle);
        menu.Opening += (_, _) =>
        {
            renderer.Palette = Palette.Current;
            menu.BackColor = renderer.Palette.Card;
        };

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };

        _timer = new System.Windows.Forms.Timer { Interval = (int)CheckInterval.TotalMilliseconds };
        _timer.Tick += (_, _) => Evaluate();
        _timer.Start();

        // Re-check immediately after waking from sleep or when the clock / time zone changes.
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;

        // A second launch of the exe signals this event instead of starting another copy.
        _showSettingsWait = ThreadPool.RegisterWaitForSingleObject(showSettingsEvent,
            (_, _) => _invoker.BeginInvoke(ShowSettings), null, Timeout.Infinite, executeOnlyOnce: false);

        Evaluate();

        if (firstRun)
        {
            _tray.ShowBalloonTip(6000, "Theme Scheduler",
                "Running in the notification area. It will start automatically with Windows.", ToolTipIcon.Info);
        }
        if (openSettings || firstRun)
            ShowSettings();
    }

    /// <summary>
    /// Applies the scheduled theme whenever the schedule crosses a boundary (or when forced).
    /// A manual "switch now" is therefore kept until the next scheduled change.
    /// </summary>
    private void Evaluate(bool force = false)
    {
        var scheduled = Schedule.DesiredAt(TimeOnly.FromDateTime(DateTime.Now), _settings.LightStart, _settings.DarkStart);

        if (_settings.Enabled && (force || scheduled != _lastScheduled))
            TryApply(scheduled);

        _lastScheduled = scheduled;
        UpdateUi(scheduled);
    }

    private void ApplyManually(ThemeMode mode)
    {
        TryApply(mode);
        UpdateUi(_lastScheduled ?? mode);
    }

    private void TryApply(ThemeMode mode)
    {
        try
        {
            ThemeManager.Apply(mode, _settings.ApplyToApps, _settings.ApplyToSystem);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            if (!_reportedApplyError)
            {
                _reportedApplyError = true;
                _tray.ShowBalloonTip(6000, "Theme Scheduler", "Couldn't change the theme: " + ex.Message, ToolTipIcon.Error);
            }
        }
    }

    private void UpdateUi(ThemeMode scheduled)
    {
        bool paused = !_settings.Enabled;
        string status = paused
            ? "Schedule paused"
            : $"{scheduled} theme until {Schedule.NextSwitch(scheduled, _settings.LightStart, _settings.DarkStart):t}";

        _statusItem.Text = status;
        _pauseItem.Text = paused ? "Resume schedule" : "Pause schedule";
        _tray.Text = Truncate("Theme Scheduler – " + status, 127);

        // Icon reflects the theme that's actually active right now (manual switches included).
        var actual = ThemeManager.GetCurrent() ?? scheduled;
        if (_iconState != (actual, paused))
        {
            var old = _icon;
            _icon = TrayIcons.Create(actual, paused);
            _tray.Icon = _icon;
            old?.Dispose();
            _iconState = (actual, paused);
        }
    }

    private void TogglePause()
    {
        _settings.Enabled = !_settings.Enabled;
        SaveSettings();
        Evaluate(force: _settings.Enabled);
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings, AutoStart.IsEnabled, OnSettingsSaved);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnSettingsSaved(AppSettings settings, bool startWithWindows)
    {
        _settings = settings;
        SaveSettings();
        try
        {
            AutoStart.Set(startWithWindows);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            MessageBox.Show("Couldn't update the startup setting: " + ex.Message, "Theme Scheduler",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Evaluate(force: true);
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Couldn't save settings: " + ex.Message, "Theme Scheduler",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            _invoker.BeginInvoke(() => Evaluate());
    }

    private void OnTimeChanged(object? sender, EventArgs e) => _invoker.BeginInvoke(() => Evaluate());

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _showSettingsWait.Unregister(null);
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        _settingsForm?.Close();
        _tray.Visible = false;
        _tray.Dispose();
        _icon?.Dispose();
        _invoker.Dispose();
        base.ExitThreadCore();
    }
}
