using System.Threading;
using System.Windows;
using CodexUsageWidget.Core;

namespace CodexUsageWidget;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private TrayIconService? _trayIconService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\CodexUsageWidget.SingleInstance",
            createdNew: out bool createdNew);

        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _ownsSingleInstanceMutex = true;
        var store = new WidgetStateStore();
        WidgetSettings settings = await store.LoadAsync();
        var startupService = new StartupRegistrationService();

        try
        {
            startupService.SetEnabled(settings.StartWithWindows);
        }
        catch
        {
            // The widget remains usable even if the registry is unavailable.
        }

        var window = new MainWindow(
            store,
            settings,
            startupService,
            new UsageCoordinator(new CodexAppServerClient(), store),
            new LocalTokenUsageReader());
        MainWindow = window;
        window.Show();
        _trayIconService = new TrayIconService(window);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
