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
                if (!TryGetTimestamp(root, out DateTimeOffset timestamp))
                {
                    continue;
                }

                latestEventAt = timestamp;
                if (root.TryGetProperty("payload", out JsonElement payload) &&
                    payload.TryGetProperty("type", out JsonElement typeElement))
                {
                    string? type = typeElement.GetString();
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
                             info.TryGetProperty("total_token_usage", out JsonElement usage))
                    {
                        latestUsage = new SessionTokenUsage(
                            usage.GetProperty("input_tokens").GetInt64(),
                            usage.GetProperty("cached_input_tokens").GetInt64(),
                            usage.GetProperty("output_tokens").GetInt64(),
                            usage.GetProperty("reasoning_output_tokens").GetInt64(),
                            usage.GetProperty("total_tokens").GetInt64(),
                            timestamp,
                            TimeSpan.Zero);
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
               DateTimeOffset.TryParse(value.GetString(), out timestamp);
    }
}
