using System.Globalization;

namespace CodexUsageWidget.Core;

public static class UsageFormatting
{
    public static string FormatReset(
        DateTimeOffset? resetTime,
        bool isWeekly,
        CultureInfo? culture = null,
        TimeZoneInfo? timeZone = null)
    {
        if (resetTime is null)
        {
            return "Reinitialisation inconnue";
        }

        culture ??= CultureInfo.CurrentCulture;
        timeZone ??= TimeZoneInfo.Local;
        DateTimeOffset local = TimeZoneInfo.ConvertTime(resetTime.Value, timeZone);
        string formatted = isWeekly
            ? local.ToString("dd MMM yyyy HH:mm", culture)
            : local.ToString("HH:mm", culture);
        return $"Reinitialisation : {formatted}";
    }
}
