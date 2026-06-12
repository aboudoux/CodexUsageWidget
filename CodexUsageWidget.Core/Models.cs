using System.Text.Json.Serialization;

namespace CodexUsageWidget.Core;

public sealed record RateLimitWindow(
    int UsedPercent,
    long? WindowDurationMins,
    long? ResetsAt)
{
    [JsonIgnore]
    public int RemainingPercent => Math.Clamp(100 - UsedPercent, 0, 100);

    [JsonIgnore]
    public bool IsLowRemaining => RemainingPercent < 20;

    [JsonIgnore]
    public DateTimeOffset? ResetTime =>
        ResetsAt is long value
            ? DateTimeOffset.FromUnixTimeSeconds(value)
            : null;
}

public sealed record UsageSnapshot(
    RateLimitWindow? Primary,
    RateLimitWindow? Secondary,
    decimal? Credits,
    string? PlanType,
    DateTimeOffset CapturedAt);

public sealed record WidgetSettings
{
    public double? Left { get; init; }
    public double? Top { get; init; }
    public bool StartWithWindows { get; init; } = true;
    public UsageSnapshot? LastSnapshot { get; init; }
}

public sealed record UsageRefreshResult(
    UsageSnapshot? Snapshot,
    bool IsStale,
    string? ErrorMessage);
