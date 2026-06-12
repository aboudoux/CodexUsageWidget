using System.Text;
using System.Text.Json;

namespace CodexUsageWidget.Core;

public sealed record SessionTokenUsage(
    long InputTokens,
    long CachedInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens,
    long TotalTokens,
    DateTimeOffset CapturedAt);

public interface ITokenUsageProvider
{
    Task<SessionTokenUsage?> ReadLatestAsync(CancellationToken cancellationToken = default);
}

public sealed class LocalTokenUsageReader : ITokenUsageProvider
{
    private const int TailSizeBytes = 2 * 1024 * 1024;
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
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);

        long start = Math.Max(0, stream.Length - TailSizeBytes);
        stream.Seek(start, SeekOrigin.Begin);
        byte[] buffer = new byte[stream.Length - start];
        int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
        string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int index = lines.Length - 1; index >= 0; index--)
        {
            string line = lines[index].Trim();
            if (!line.Contains("\"type\":\"token_count\"", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                JsonElement usage = root
                    .GetProperty("payload")
                    .GetProperty("info")
                    .GetProperty("total_token_usage");

                DateTimeOffset capturedAt = root.TryGetProperty("timestamp", out JsonElement timestamp) &&
                    DateTimeOffset.TryParse(timestamp.GetString(), out DateTimeOffset parsed)
                        ? parsed
                        : new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);

                return new SessionTokenUsage(
                    usage.GetProperty("input_tokens").GetInt64(),
                    usage.GetProperty("cached_input_tokens").GetInt64(),
                    usage.GetProperty("output_tokens").GetInt64(),
                    usage.GetProperty("reasoning_output_tokens").GetInt64(),
                    usage.GetProperty("total_tokens").GetInt64(),
                    capturedAt);
            }
            catch (JsonException)
            {
                // The app can read while Codex is still writing the final line.
            }
        }

        return null;
    }
}
