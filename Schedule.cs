namespace ThemeScheduler;

internal enum ThemeMode { Light, Dark }

internal static class Schedule
{
    /// <summary>Which theme the schedule wants at the given time of day. Handles windows that wrap past midnight.</summary>
    public static ThemeMode DesiredAt(TimeOnly now, TimeOnly lightStart, TimeOnly darkStart)
    {
        if (lightStart == darkStart)
            return ThemeMode.Light;

        if (lightStart < darkStart)
            return now >= lightStart && now < darkStart ? ThemeMode.Light : ThemeMode.Dark;

        // Light period wraps midnight (e.g. light 20:00 -> dark 06:00).
        return now >= darkStart && now < lightStart ? ThemeMode.Dark : ThemeMode.Light;
    }

    /// <summary>The time at which the current period ends.</summary>
    public static TimeOnly NextSwitch(ThemeMode current, TimeOnly lightStart, TimeOnly darkStart) =>
        current == ThemeMode.Light ? darkStart : lightStart;
}
