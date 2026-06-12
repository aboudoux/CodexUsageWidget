using CodexUsageWidget.Core;

namespace CodexUsageWidget.Tests;

[TestClass]
public sealed class UsageCoordinatorTests
{
    [TestMethod]
    public async Task Refresh_SuccessStoresFreshSnapshot()
    {
        var snapshot = CreateSnapshot(20);
        var provider = new FakeProvider(_ => Task.FromResult(snapshot));
        var store = new MemoryStore();
        var coordinator = new UsageCoordinator(provider, store);

        UsageRefreshResult result = await coordinator.RefreshAsync(new WidgetSettings());

        Assert.AreEqual(snapshot, result.Snapshot);
        Assert.IsFalse(result.IsStale);
        Assert.AreEqual(snapshot, store.Settings.LastSnapshot);
    }

    [TestMethod]
    public async Task Refresh_ErrorReturnsCachedSnapshotAsStale()
    {
        var cached = CreateSnapshot(30);
        var provider = new FakeProvider(_ => throw new InvalidOperationException("offline"));
        var store = new MemoryStore();
        var coordinator = new UsageCoordinator(provider, store);

        UsageRefreshResult result = await coordinator.RefreshAsync(
            new WidgetSettings { LastSnapshot = cached });

        Assert.AreEqual(cached, result.Snapshot);
        Assert.IsTrue(result.IsStale);
        StringAssert.Contains(result.ErrorMessage, "offline");
    }

    [TestMethod]
    public async Task Refresh_TimeoutReturnsCachedSnapshotAsStale()
    {
        var cached = CreateSnapshot(40);
        var provider = new FakeProvider(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return cached;
        });
        var coordinator = new UsageCoordinator(
            provider,
            new MemoryStore(),
            TimeSpan.FromMilliseconds(20));

        UsageRefreshResult result = await coordinator.RefreshAsync(
            new WidgetSettings { LastSnapshot = cached });

        Assert.AreEqual(cached, result.Snapshot);
        Assert.IsTrue(result.IsStale);
        StringAssert.Contains(result.ErrorMessage, "delai");
    }

    private static UsageSnapshot CreateSnapshot(int usedPercent) =>
        new(
            new RateLimitWindow(usedPercent, 300, null),
            new RateLimitWindow(usedPercent, 10080, null),
            null,
            "plus",
            DateTimeOffset.UnixEpoch);

    private sealed class FakeProvider(
        Func<CancellationToken, Task<UsageSnapshot>> callback) : IUsageProvider
    {
        public Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken) =>
            callback(cancellationToken);
    }

    private sealed class MemoryStore : IWidgetStateStore
    {
        public WidgetSettings Settings { get; private set; } = new();

        public Task<WidgetSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task SaveAsync(
            WidgetSettings settings,
            CancellationToken cancellationToken = default)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }
}
