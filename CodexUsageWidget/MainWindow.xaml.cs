using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CodexUsageWidget.Core;

namespace CodexUsageWidget;

public partial class MainWindow : Window
{
    private const string AnalyticsUrl = "https://chatgpt.com/codex/cloud/settings/analytics#usage";

    private readonly IWidgetStateStore _store;
    private readonly StartupRegistrationService _startupService;
    private readonly UsageCoordinator _coordinator;
    private readonly ITokenUsageProvider _tokenUsageProvider;
    private readonly DispatcherTimer _timer;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private WidgetSettings _settings;
    private bool _isClosing;

    public event EventHandler<string>? UsageSummaryChanged;

    public bool StartWithWindows => _settings.StartWithWindows;

    public MainWindow(
        IWidgetStateStore store,
        WidgetSettings settings,
        StartupRegistrationService startupService,
        UsageCoordinator coordinator,
        ITokenUsageProvider tokenUsageProvider)
    {
        InitializeComponent();
        _store = store;
        _settings = settings;
        _startupService = startupService;
        _coordinator = coordinator;
        _tokenUsageProvider = tokenUsageProvider;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        StartWithWindowsMenuItem.IsChecked = settings.StartWithWindows;
        RestorePosition(settings);
        if (settings.LastSnapshot is not null)
        {
            RenderSnapshot(settings.LastSnapshot);
            SetStatus("Derniere mesure enregistree", isError: false);
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        await RefreshAsync();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        e.Cancel = true;
        _timer.Stop();
        _settings = _settings with { Left = Left, Top = Top };
        try
        {
            await _store.SaveAsync(_settings);
        }
        catch
        {
            // Closing should not be blocked by a settings write failure.
        }

        _isClosing = true;
        Close();
    }

    public async Task RefreshAsync()
    {
        if (!await _refreshLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            SetStatus("Actualisation...", isError: false);
            Task<UsageRefreshResult> usageTask = _coordinator.RefreshAsync(_settings);
            Task<SessionTokenUsage?> tokenTask = _tokenUsageProvider.ReadLatestAsync();
            UsageRefreshResult result = await usageTask;
            SessionTokenUsage? tokenUsage = await tokenTask;
            RenderTokenUsage(tokenUsage);

            if (result.Snapshot is not null)
            {
                RenderSnapshot(result.Snapshot);
                if (!result.IsStale)
                {
                    _settings = _settings with { LastSnapshot = result.Snapshot };
                }
            }

            if (result.IsStale)
            {
                SetStatus("Donnees enregistrees - Codex indisponible", isError: true);
            }
            else if (result.Snapshot is null)
            {
                SetStatus("Donnees indisponibles", isError: true);
            }
            else
            {
                string refreshedAt = result.Snapshot.CapturedAt
                    .ToLocalTime()
                    .ToString("HH:mm", CultureInfo.CurrentCulture);
                SetStatus($"Mis a jour a {refreshedAt}", isError: false);
            }

            ToolTip = result.ErrorMessage;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void RenderTokenUsage(SessionTokenUsage? usage)
    {
        TokensText.Text = usage is null
            ? "--"
            : FormatCompactNumber(usage.TotalTokens);
        TokensText.ToolTip = usage is null
            ? "Aucune conversation Codex locale trouvee."
            : $"Entree : {usage.InputTokens:N0}\n" +
              $"Dont cache : {usage.CachedInputTokens:N0}\n" +
              $"Sortie : {usage.OutputTokens:N0}\n" +
              $"Raisonnement : {usage.ReasoningOutputTokens:N0}";
    }

    private static string FormatCompactNumber(long value) =>
        value switch
        {
            >= 1_000_000 => $"{value / 1_000_000d:0.00} M",
            >= 1_000 => $"{value / 1_000d:0.0} k",
            _ => value.ToString("N0", CultureInfo.CurrentCulture)
        };

    private void RenderSnapshot(UsageSnapshot snapshot)
    {
        RenderWindow(
            snapshot.WeeklyLimit,
            WeeklyPercentText,
            WeeklyProgress,
            WeeklyResetText);

        CreditsText.Text = snapshot.Credits is decimal credits
            ? Math.Floor(credits).ToString("N0", CultureInfo.CurrentCulture)
            : "--";

        string weekly = snapshot.WeeklyLimit is null
            ? "--"
            : $"{snapshot.WeeklyLimit.RemainingPercent}%";
        UsageSummaryChanged?.Invoke(this, $"Codex: semaine {weekly}");
    }

    private static void RenderWindow(
        RateLimitWindow? window,
        System.Windows.Controls.TextBlock percentText,
        System.Windows.Controls.ProgressBar progress,
        System.Windows.Controls.TextBlock resetText)
    {
        if (window is null)
        {
            percentText.Text = "-- %";
            progress.Value = 0;
            progress.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(46, 204, 113));
            percentText.Foreground = System.Windows.Media.Brushes.White;
            resetText.Text = "Reinitialisation inconnue";
            return;
        }

        percentText.Text = $"{window.RemainingPercent} %";
        progress.Value = window.RemainingPercent;
        System.Windows.Media.Brush usageBrush = window.IsLowRemaining
            ? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 72, 77))
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(46, 204, 113));
        progress.Foreground = usageBrush;
        percentText.Foreground = usageBrush;
        resetText.Text = UsageFormatting.FormatReset(window.ResetTime, isWeekly: true);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? System.Windows.Media.Brushes.Orange
            : (System.Windows.Media.Brush)FindResource("MutedBrush");
    }

    private void RestorePosition(WidgetSettings settings)
    {
        if (settings.Left is double left &&
            settings.Top is double top &&
            IsVisibleOnDesktop(left, top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = SystemParameters.WorkArea.Right - Width - 20;
        Top = SystemParameters.WorkArea.Top + 20;
    }

    private bool IsVisibleOnDesktop(double left, double top) =>
        left + Width >= SystemParameters.VirtualScreenLeft &&
        left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
        top + Height >= SystemParameters.VirtualScreenTop &&
        top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void RefreshMenuItem_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void OpenAnalyticsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenAnalytics();
    }

    public void OpenAnalytics()
    {
        Process.Start(new ProcessStartInfo(AnalyticsUrl) { UseShellExecute = true });
    }

    private async void StartWithWindowsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await SetStartWithWindowsAsync(StartWithWindowsMenuItem.IsChecked);
    }

    public async Task<bool> SetStartWithWindowsAsync(bool enabled)
    {
        try
        {
            _startupService.SetEnabled(enabled);
            _settings = _settings with { StartWithWindows = enabled };
            StartWithWindowsMenuItem.IsChecked = enabled;
            await _store.SaveAsync(_settings);
            SetStatus(
                enabled ? "Demarrage automatique active" : "Demarrage automatique desactive",
                isError: false);
            return true;
        }
        catch (Exception exception)
        {
            StartWithWindowsMenuItem.IsChecked = _settings.StartWithWindows;
            SetStatus("Impossible de modifier le demarrage", isError: true);
            ToolTip = exception.Message;
            return false;
        }
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
        Activate();
        Topmost = true;
    }

    public void ExitApplication() => Close();

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => ExitApplication();
}
