using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ArchiveTransparency.Helpers;
using ArchiveTransparency.Services;
using ArchiveTransparency.Windows;

namespace ArchiveTransparency;

public partial class App : System.Windows.Application
{
    private NotifyIcon? _trayIcon;
    private ExplorerMonitor? _monitor;
    private TooltipWindow? _tooltipWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _tooltipWindow = new TooltipWindow();
        _monitor = new ExplorerMonitor(_tooltipWindow);
        _monitor.Start();

        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "Archive Transparency",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        var titleItem = _trayIcon.ContextMenuStrip.Items.Add("📦 Archive Transparency");
        titleItem.Enabled = false;
        titleItem.Font = new Font(titleItem.Font, System.Drawing.FontStyle.Bold);

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

        var statusItem = _trayIcon.ContextMenuStrip.Items.Add("🟢 Активно");
        statusItem.Enabled = false;

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

        string autoLabel = AutoStartHelper.IsEnabled ? "✅ Автозапуск" : "⬜ Автозапуск";
        var autoStartItem = _trayIcon.ContextMenuStrip.Items.Add(autoLabel);
        autoStartItem.Click += (_, _) =>
        {
            AutoStartHelper.Toggle();
            autoStartItem.Text = AutoStartHelper.IsEnabled ? "✅ Автозапуск" : "⬜ Автозапуск";
        };

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

        _trayIcon.ContextMenuStrip.Items.Add("❌ Выход", null, (_, _) =>
        {
            _monitor?.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Shutdown();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Stop();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.OnExit(e);
    }
}
