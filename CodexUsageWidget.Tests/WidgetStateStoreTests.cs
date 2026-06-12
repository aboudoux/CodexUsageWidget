using CodexUsageWidget.Core;

namespace CodexUsageWidget.Tests;

[TestClass]
public sealed class WidgetStateStoreTests
{
    [TestMethod]
    public async Task SaveAndLoad_PreservesPositionPreferenceAndSnapshot()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "settings.json");

        try
        {
            var store = new WidgetStateStore(filePath);
            var snapshot = new UsageSnapshot(
                new RateLimitWindow(25, 300, 123456),
                null,
                42.5m,
                "plus",
                DateTimeOffset.UnixEpoch);
            var expected = new WidgetSettings
            {
                Left = 120,
                Top = 80,
                StartWithWindows = false,
                LastSnapshot = snapshot
            };

            await store.SaveAsync(expected);
            WidgetSettings actual = await store.LoadAsync();

            Assert.AreEqual(expected, actual);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task Load_InvalidJson_ReturnsDefaults()
    {
        string filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "{ invalid");
            var store = new WidgetStateStore(filePath);

            WidgetSettings settings = await store.LoadAsync();

            Assert.IsTrue(settings.StartWithWindows);
            Assert.IsNull(settings.LastSnapshot);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
