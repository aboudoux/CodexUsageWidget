using System.Globalization;
using System.Text.Json;

namespace CodexUsageWidget.Core;

public static class CodexRateLimitParser
{
    public static UsageSnapshot Parse(JsonElement result, DateTimeOffset capturedAt)
    {
        JsonElement limits = result.GetProperty("rateLimits");

        return new UsageSnapshot(
            ParseWindow(limits, "primary"),
            ParseWindow(limits, "secondary"),
            ParseCredits(limits),
            GetOptionalString(limits, "planType"),
            capturedAt);
    }

    private static RateLimitWindow? ParseWindow(JsonElement limits, string propertyName)
    {
        if (!limits.TryGetProperty(propertyName, out JsonElement window) ||
            window.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        int usedPercent = window.GetProperty("usedPercent").GetInt32();
        return new RateLimitWindow(
            usedPercent,
            GetOptionalInt64(window, "windowDurationMins"),
            GetOptionalInt64(window, "resetsAt"));
    }

    private static decimal? ParseCredits(JsonElement limits)
    {
        if (!limits.TryGetProperty("credits", out JsonElement credits) ||
            credits.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !credits.TryGetProperty("balance", out JsonElement balance) ||
            balance.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        string? raw = balance.GetString();
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : null;
    }

    private static long? GetOptionalInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.Number
            ? property.GetInt64()
            : null;

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
