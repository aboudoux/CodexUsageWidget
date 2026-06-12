using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace CodexUsageWidget;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _visibilityItem;
    private readonly Forms.ToolStripMenuItem _startupItem;

    public TrayIconService(MainWindow window)
    {
        _window = window;
        _visibilityItem = new Forms.ToolStripMenuItem("Masquer le widget");
        _startupItem = new Forms.ToolStripMenuItem("Demarrer avec Windows")
        {
            Checked = window.StartWithWindows,
            CheckOnClick = true
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_visibilityItem);
        menu.Items.Add("Actualiser", null, async (_, _) => await _window.RefreshAsync());
        menu.Items.Add("Ouvrir Codex Analytics", null, (_, _) => _window.OpenAnalytics());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => _window.ExitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "Codex Usage Widget",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        _visibilityItem.Click += (_, _) => ToggleWindow();
        _startupItem.Click += StartupItem_Click;
        _window.IsVisibleChanged += Window_IsVisibleChanged;
        _window.UsageSummaryChanged += Window_UsageSummaryChanged;
    }

    private void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            ToggleWindow();
        }
    }

    private void ToggleWindow()
    {
        _window.ToggleVisibility();
        UpdateVisibilityText();
    }

    private async void StartupItem_Click(object? sender, EventArgs e)
    {
        bool requested = _startupItem.Checked;
        bool succeeded = await _window.SetStartWithWindowsAsync(requested);
        if (!succeeded)
        {
            _startupItem.Checked = _window.StartWithWindows;
        }
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        UpdateVisibilityText();

    private void Window_UsageSummaryChanged(object? sender, string summary)
    {
        _notifyIcon.Text = summary.Length <= 63
            ? summary
            : summary[..63];
    }

    private void UpdateVisibilityText()
    {
        _visibilityItem.Text = _window.IsVisible
            ? "Masquer le widget"
            : "Afficher le widget";
    }

    public void Dispose()
    {
        _window.IsVisibleChanged -= Window_IsVisibleChanged;
        _window.UsageSummaryChanged -= Window_UsageSummaryChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
