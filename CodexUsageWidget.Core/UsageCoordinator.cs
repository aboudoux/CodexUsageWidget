namespace CodexUsageWidget.Core;

public sealed class UsageCoordinator
{
    private readonly IUsageProvider _provider;
    private readonly IWidgetStateStore _store;
    private readonly TimeSpan _timeout;

    public UsageCoordinator(
        IUsageProvider provider,
        IWidgetStateStore store,
        TimeSpan? timeout = null)
    {
        _provider = provider;
        _store = store;
        _timeout = timeout ?? TimeSpan.FromSeconds(12);
    }

    public async Task<UsageRefreshResult> RefreshAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        try
        {
            UsageSnapshot snapshot = await _provider.ReadAsync(timeout.Token);
            await _store.SaveAsync(settings with { LastSnapshot = snapshot }, cancellationToken);
            return new UsageRefreshResult(snapshot, IsStale: false, ErrorMessage: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UsageRefreshResult(
                settings.LastSnapshot,
                IsStale: settings.LastSnapshot is not null,
                ErrorMessage: "Actualisation impossible: delai depasse.");
        }
        catch (Exception exception)
        {
            return new UsageRefreshResult(
                settings.LastSnapshot,
                IsStale: settings.LastSnapshot is not null,
                ErrorMessage: exception.Message);
        }
    }
}
