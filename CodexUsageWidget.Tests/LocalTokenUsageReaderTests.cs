using CodexUsageWidget.Core;

namespace CodexUsageWidget.Tests;

[TestClass]
public sealed class LocalTokenUsageReaderTests
{
    [TestMethod]
    public async Task ReadLatestAsync_ReturnsLastTokenCountFromNewestSession()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string older = Path.Combine(directory, "older.jsonl");
            string newer = Path.Combine(directory, "newer.jsonl");
            await File.WriteAllTextAsync(older, CreateLine(10));
            await File.WriteAllTextAsync(newer, CreateLine(123456));
            File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddMinutes(-1));
            File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

            var reader = new LocalTokenUsageReader(directory);
            SessionTokenUsage? usage = await reader.ReadLatestAsync();

            Assert.IsNotNull(usage);
            Assert.AreEqual(123456, usage.TotalTokens);
            Assert.AreEqual(100, usage.InputTokens);
            Assert.AreEqual(40, usage.CachedInputTokens);
            Assert.AreEqual(20, usage.OutputTokens);
            Assert.AreEqual(5, usage.ReasoningOutputTokens);
            Assert.AreEqual(TimeSpan.Zero, usage.TotalWorkedTime);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadLatestAsync_SumsCompletedAndActiveTaskDurations()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string session = Path.Combine(directory, "session.jsonl");
            string contents = string.Join('\n',
                CreateEvent("2026-06-12T08:00:00Z", "task_started"),
                CreateEvent("2026-06-12T08:02:30Z", "task_complete"),
                CreateEvent("2026-06-12T12:00:00Z", "task_started"),
                CreateLine(500, "2026-06-12T12:01:15Z"));
            await File.WriteAllTextAsync(session, contents);

            SessionTokenUsage? usage = await new LocalTokenUsageReader(directory).ReadLatestAsync();

            Assert.IsNotNull(usage);
            Assert.AreEqual(TimeSpan.FromMinutes(3.75), usage.TotalWorkedTime);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadLatestAsync_MissingDirectoryReturnsNull()
    {
        var reader = new LocalTokenUsageReader(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        SessionTokenUsage? usage = await reader.ReadLatestAsync();

        Assert.IsNull(usage);
    }

    [TestMethod]
    public async Task ReadLatestAsync_IgnoresNullAndMalformedPayloads()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string session = Path.Combine(directory, "session.jsonl");
            string contents = string.Join('\n',
                """{"timestamp":"2026-06-12T08:00:00Z","type":"event_msg","payload":null}""",
                """{"timestamp":"2026-06-12T08:00:01Z","type":"event_msg","payload":{"type":"token_count","info":null}}""",
                """{"timestamp":"2026-06-12T08:00:02Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":null}}}""",
                CreateLine(789, "2026-06-12T08:00:03Z"));
            await File.WriteAllTextAsync(session, contents);

            SessionTokenUsage? usage = await new LocalTokenUsageReader(directory).ReadLatestAsync();

            Assert.IsNotNull(usage);
            Assert.AreEqual(789, usage.TotalTokens);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateLine(long totalTokens, string timestamp = "2026-06-12T08:22:21.090Z") =>
        """
        {"timestamp":"TIMESTAMP","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":100,"cached_input_tokens":40,"output_tokens":20,"reasoning_output_tokens":5,"total_tokens":TOTAL}}}}
        """.Replace("TIMESTAMP", timestamp)
           .Replace("TOTAL", totalTokens.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string CreateEvent(string timestamp, string type) =>
        """
        {"timestamp":"TIMESTAMP","type":"event_msg","payload":{"type":"TYPE"}}
        """.Replace("TIMESTAMP", timestamp).Replace("TYPE", type);
}
