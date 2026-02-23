using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using ArchiveTransparency.Config;
using ArchiveTransparency.DependencyInjection;
using ArchiveTransparency.Helpers;
using ArchiveTransparency.Services;
using ArchiveTransparency.Windows;
using Serilog;

namespace ArchiveTransparency;

public partial class App : System.Windows.Application
{
    private readonly ILogger _logger;
    private readonly Settings _settings;
    private readonly StatisticsService _statistics;
    private NotifyIcon? _trayIcon;
    private ExplorerMonitor? _monitor;
    private TooltipWindow? _tooltipWindow;
    private HotkeyService? _hotkeyService;

    public App()
    {
        // Initialize DI container first
        var services = ServiceContainer.Services;
        
        _logger = services.GetRequiredService<ILogger>();
        _settings = services.GetRequiredService<Settings>();
        _statistics = services.GetRequiredService<StatisticsService>();
        
        _logger.Information("Application starting");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger.Information("Application startup");

        _tooltipWindow = ServiceContainer.GetRequiredService<TooltipWindow>();
        _monitor = ServiceContainer.GetRequiredService<ExplorerMonitor>();
        _hotkeyService = ServiceContainer.GetRequiredService<HotkeyService>();
        
        _monitor.Start();
        
        // Register hotkey
        _hotkeyService.RegisterHotkey(_settings.HotkeyShow, "ShowArchive");
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        SetupTrayIcon();
        
        _logger.Information("Application started successfully");
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        _logger.Information("Hotkey pressed: {Name}", e.Name);
        // Hotkey handling - could force show tooltip for current hovered item
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

        // Status item
        var statusItem = _trayIcon.ContextMenuStrip.Items.Add("🟢 Активно");
        statusItem.Enabled = false;

        // Statistics item (if enabled)
        ToolStripMenuItem? statsItem = null;
        if (_settings.ShowStatistics)
        {
            statsItem = _trayIcon.ContextMenuStrip.Items.Add($"📊 {_statistics.GetSummary()}") as ToolStripMenuItem;
            statsItem.Enabled = false;
        }

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

        // Auto-start toggle
        string autoLabel = AutoStartHelper.IsEnabled ? "✅ Автозапуск" : "⬜ Автозапуск";
        var autoStartItem = _trayIcon.ContextMenuStrip.Items.Add(autoLabel);
        autoStartItem.Click += (_, _) =>
        {
            AutoStartHelper.Toggle();
            autoStartItem.Text = AutoStartHelper.IsEnabled ? "✅ Автозапуск" : "⬜ Автозапуск";
            _logger.Information("Auto-start toggled: {Enabled}", AutoStartHelper.IsEnabled);
        };

        // Reset statistics
        var resetStatsItem = _trayIcon.ContextMenuStrip.Items.Add("🔄 Сброс статистики") as ToolStripMenuItem;
        resetStatsItem.Click += (_, _) =>
        {
            _statistics.Reset();
            if (statsItem != null)
            {
                statsItem.Text = $"📊 {_statistics.GetSummary()}";
            }
            _logger.Information("Statistics reset");
        };

        _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

        // Exit
        _trayIcon.ContextMenuStrip.Items.Add("❌ Выход", null, (_, _) =>
        {
            _logger.Information("Application exiting from tray menu");
            Shutdown();
        });

        // Update statistics periodically
        var statsTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        statsTimer.Tick += (_, _) =>
        {
            if (statsItem != null && _settings.ShowStatistics)
            {
                statsItem.Text = $"📊 {_statistics.GetSummary()}";
            }
        };
        statsTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger.Information("Application exiting");
        
        _monitor?.Stop();
        _hotkeyService?.Dispose();
        
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        
        Log.Information("Application shutdown complete");
        Log.CloseAndFlush();
        
        base.OnExit(e);
    }
}
