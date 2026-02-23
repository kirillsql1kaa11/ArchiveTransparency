using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;
using ArchiveTransparency.Config;
using ArchiveTransparency.Helpers;
using ArchiveTransparency.Windows;
using Serilog;
using static ArchiveTransparency.Helpers.NativeMethods;

namespace ArchiveTransparency.Services;

public class ExplorerMonitor
{
    private readonly ILogger _logger;
    private readonly Settings _settings;
    private readonly TooltipWindow _tooltip;
    private readonly ArchiveReader _archiveReader;
    private readonly StatisticsService _statistics;
    private DispatcherTimer? _timer;
    private string? _lastArchivePath;
    private bool _isProcessing;
    private CancellationTokenSource? _cts;
    private DateTime _lastTriggerTime;

    private static readonly string[] ArchiveExtensions =
    [
        ".zip", ".rar", ".7z",
        ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".xz", ".txz",
        ".cab", ".iso", ".wim", ".lzh", ".lzma", ".arj"
    ];

    public ExplorerMonitor(
        ILogger logger,
        Settings settings,
        TooltipWindow tooltip,
        ArchiveReader archiveReader,
        StatisticsService statistics)
    {
        _logger = logger;
        _settings = settings;
        _tooltip = tooltip;
        _archiveReader = archiveReader;
        _statistics = statistics;
        _lastTriggerTime = DateTime.MinValue;

        _logger.Information("ExplorerMonitor initialized with {Interval}ms polling, {Debounce}ms debounce",
            settings.PollingIntervalMs, settings.DebounceIntervalMs);
    }

    public void Start()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_settings.PollingIntervalMs) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
        _logger.Information("ExplorerMonitor started");
    }

    public void Stop()
    {
        _timer?.Stop();
        _cts?.Cancel();
        _logger.Information("ExplorerMonitor stopped");
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        // Debouncing
        var now = DateTime.Now;
        if ((now - _lastTriggerTime).TotalMilliseconds < _settings.DebounceIntervalMs)
            return;

        if (_isProcessing) return;

        try
        {
            _isProcessing = true;
            _lastTriggerTime = now;

            GetCursorPos(out POINT pt);

            // Don't hide if cursor is over the tooltip itself
            if (_tooltip.IsVisible && _tooltip.IsHitTestVisible && IsPointOverTooltip(pt))
                return;

            IntPtr hwnd = WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) { HideTooltip(); return; }

            IntPtr rootHwnd = GetAncestor(hwnd, GA_ROOT);
            if (!IsExplorerWindow(rootHwnd)) { HideTooltip(); return; }

            string? itemName = GetItemNameAtPoint(pt);
            if (string.IsNullOrEmpty(itemName)) { HideTooltip(); return; }

            string? folderPath = GetFolderPath(rootHwnd);
            if (string.IsNullOrEmpty(folderPath)) { HideTooltip(); return; }

            string? archivePath = FindArchiveFile(folderPath, itemName);
            if (archivePath == null) { HideTooltip(); return; }

            // Same archive — just update position
            if (archivePath == _lastArchivePath && _tooltip.IsVisible)
                return;

            // New archive detected
            _lastArchivePath = archivePath;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _logger.Debug("Detected archive: {ArchivePath}", archivePath);

            _tooltip.ShowLoading(Path.GetFileName(archivePath), pt.X + 16, pt.Y + 16);

            try
            {
                var entries = await _archiveReader.ReadArchiveAsync(archivePath, token);
                if (!token.IsCancellationRequested)
                    _tooltip.ShowEntries(Path.GetFileName(archivePath), entries, pt.X + 16, pt.Y + 16);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Read operation cancelled for {ArchivePath}", archivePath);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error in ExplorerMonitor tick");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private bool IsPointOverTooltip(POINT pt)
    {
        try
        {
            double left = _tooltip.Left, top = _tooltip.Top;
            double right = left + _tooltip.ActualWidth;
            double bottom = top + _tooltip.ActualHeight;
            return pt.X >= left && pt.X <= right && pt.Y >= top && pt.Y <= bottom;
        }
        catch { return false; }
    }

    private void HideTooltip()
    {
        if (_tooltip.IsVisible)
        {
            _tooltip.HideTooltip();
            _lastArchivePath = null;
            _cts?.Cancel();
        }
    }

    private static bool IsExplorerWindow(IntPtr hwnd)
    {
        string className = GetWindowClassName(hwnd);
        if (className is "CabinetWClass" or "ExploreWClass" or "Progman" or "WorkerW")
            return true;

        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to check process for HWND {Hwnd}", hwnd);
            return false;
        }
    }

    private static string? GetItemNameAtPoint(POINT pt)
    {
        try
        {
            var point = new System.Windows.Point(pt.X, pt.Y);
            var element = AutomationElement.FromPoint(point);

            int depth = 0;
            while (element != null && depth < 10)
            {
                var ct = element.Current.ControlType;
                if (ct == ControlType.DataItem || ct == ControlType.ListItem)
                    return element.Current.Name;
                element = TreeWalker.RawViewWalker.GetParent(element);
                depth++;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "UIAutomation failed to get item name");
        }
        return null;
    }

    private static string? GetFolderPath(IntPtr explorerHwnd)
    {
        string className = GetWindowClassName(explorerHwnd);

        // Desktop
        if (className is "Progman" or "WorkerW")
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // Explorer window — use Shell COM
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return null;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic windows = shell.Windows();

            try
            {
                for (int i = 0; i < windows.Count; i++)
                {
                    try
                    {
                        dynamic? window = windows.Item(i);
                        if (window == null) continue;

                        IntPtr wHwnd = (IntPtr)(long)window.HWND;
                        if (wHwnd != explorerHwnd) continue;

                        string? url = window.LocationURL;
                        if (!string.IsNullOrEmpty(url))
                            return new Uri(url).LocalPath;
                    }
                    catch { continue; }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(windows);
                Marshal.ReleaseComObject(shell);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "COM failed to get folder path");
        }

        return null;
    }

    private static string? FindArchiveFile(string folderPath, string itemName)
    {
        // Direct match (extensions visible)
        string direct = Path.Combine(folderPath, itemName);
        if (File.Exists(direct) && IsArchiveExtension(Path.GetExtension(direct)))
            return direct;

        // Try adding archive extensions (extensions hidden in Explorer)
        foreach (var ext in ArchiveExtensions)
        {
            string candidate = Path.Combine(folderPath, itemName + ext);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsArchiveExtension(string ext)
        => ArchiveExtensions.Contains(ext.ToLowerInvariant());
}
