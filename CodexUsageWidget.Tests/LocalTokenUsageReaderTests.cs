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

    private static string CreateLine(long totalTokens) =>
        """
        {"timestamp":"2026-06-12T08:22:21.090Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":100,"cached_input_tokens":40,"output_tokens":20,"reasoning_output_tokens":5,"total_tokens":TOTAL}}}}
        """.Replace("TOTAL", totalTokens.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
