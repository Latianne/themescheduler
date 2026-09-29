namespace ThemeScheduler;

internal static class Program
{
    private const string MutexName = @"Local\ThemeScheduler.Instance";
    internal const string ShowSettingsEventName = @"Local\ThemeScheduler.ShowSettings";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Already running: ask the existing instance to show its settings window.
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ShowSettingsEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException) { }
            return;
        }

        ApplicationConfiguration.Initialize();

        using var showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
        bool launchedAtStartup = args.Contains(AutoStart.StartupArgument, StringComparer.OrdinalIgnoreCase);

        Application.Run(new TrayContext(showSettingsEvent, openSettings: !launchedAtStartup));
    }
}
