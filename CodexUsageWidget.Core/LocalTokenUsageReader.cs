using System.Text;
using System.Text.Json;

namespace CodexUsageWidget.Core;

public sealed record SessionTokenUsage(
    long InputTokens,
    long CachedInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens,
    long TotalTokens,
    DateTimeOffset CapturedAt,
    TimeSpan TotalWorkedTime);

public interface ITokenUsageProvider
{
    Task<SessionTokenUsage?> ReadLatestAsync(CancellationToken cancellationToken = default);
}

public sealed class LocalTokenUsageReader : ITokenUsageProvider
{
    private readonly string _sessionsPath;

    public LocalTokenUsageReader(string? sessionsPath = null)
    {
        _sessionsPath = sessionsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");
    }

    public async Task<SessionTokenUsage?> ReadLatestAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_sessionsPath))
        {
            return null;
        }

        FileInfo? latest = new DirectoryInfo(_sessionsPath)
            .EnumerateFiles("*.jsonl", SearchOption.AllDirectories)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        return latest is null
            ? null
            : await ReadFileTailAsync(latest.FullName, cancellationToken);
    }

    internal static async Task<SessionTokenUsage?> ReadFileTailAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        SessionTokenUsage? latestUsage = null;
        DateTimeOffset? taskStartedAt = null;
        DateTimeOffset? latestEventAt = null;
        TimeSpan totalWorkedTime = TimeSpan.Zero;

        while (await reader.ReadLineAsync(cancellationToken) is string rawLine)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !TryGetTimestamp(root, out DateTimeOffset timestamp))
                {
                    continue;
                }

                latestEventAt = timestamp;
                if (root.TryGetProperty("payload", out JsonElement payload) &&
                    payload.ValueKind == JsonValueKind.Object &&
                    payload.TryGetProperty("type", out JsonElement typeElement))
                {
                    string? type = typeElement.ValueKind == JsonValueKind.String
                        ? typeElement.GetString()
                        : null;
                    if (type == "task_started")
                    {
                        if (taskStartedAt is DateTimeOffset previousStart && timestamp > previousStart)
                        {
                            totalWorkedTime += timestamp - previousStart;
                        }
                        taskStartedAt = timestamp;
                    }
                    else if ((type == "task_complete" || type == "turn_aborted") &&
                             taskStartedAt is DateTimeOffset start)
                    {
                        if (timestamp > start)
                        {
                            totalWorkedTime += timestamp - start;
                        }
                        taskStartedAt = null;
                    }
                    else if (type == "token_count" &&
                             payload.TryGetProperty("info", out JsonElement info) &&
                             info.ValueKind == JsonValueKind.Object &&
                             info.TryGetProperty("total_token_usage", out JsonElement usage) &&
                             TryReadTokenUsage(usage, timestamp, out SessionTokenUsage? tokenUsage))
                    {
                        latestUsage = tokenUsage;
                    }
                }
            }
            catch (JsonException)
            {
                // The app can read while Codex is still writing the final line.
            }
        }

        if (taskStartedAt is DateTimeOffset activeStart && latestEventAt > activeStart)
        {
            totalWorkedTime += latestEventAt.Value - activeStart;
        }

        return latestUsage is null
            ? null
            : latestUsage with { TotalWorkedTime = totalWorkedTime };
    }

    private static bool TryGetTimestamp(JsonElement root, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return root.TryGetProperty("timestamp", out JsonElement value) &&
               value.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(value.GetString(), out timestamp);
    }

    private static bool TryReadTokenUsage(
        JsonElement usage,
        DateTimeOffset timestamp,
        out SessionTokenUsage? tokenUsage)
    {
        tokenUsage = null;
        if (usage.ValueKind != JsonValueKind.Object ||
            !TryGetInt64(usage, "input_tokens", out long inputTokens) ||
            !TryGetInt64(usage, "cached_input_tokens", out long cachedInputTokens) ||
            !TryGetInt64(usage, "output_tokens", out long outputTokens) ||
            !TryGetInt64(usage, "reasoning_output_tokens", out long reasoningOutputTokens) ||
            !TryGetInt64(usage, "total_tokens", out long totalTokens))
        {
            return false;
        }

        tokenUsage = new SessionTokenUsage(
            inputTokens,
            cachedInputTokens,
            outputTokens,
            reasoningOutputTokens,
            totalTokens,
            timestamp,
            TimeSpan.Zero);
        return true;
    }

    private static bool TryGetInt64(JsonElement parent, string propertyName, out long value)
    {
        value = default;
        return parent.TryGetProperty(propertyName, out JsonElement element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt64(out value);
    }
}
