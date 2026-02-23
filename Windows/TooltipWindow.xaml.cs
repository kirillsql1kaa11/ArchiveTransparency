using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ArchiveTransparency.Config;
using ArchiveTransparency.Helpers;
using ArchiveTransparency.Models;
using Serilog;
using Clipboard = System.Windows.Clipboard;

namespace ArchiveTransparency.Windows;

public partial class TooltipWindow : Window
{
    private readonly ILogger _logger;
    private readonly Settings _settings;
    private string? _currentArchivePath;

    private const int MaxDisplayEntries = 200;

    public TooltipWindow() : this(Log.Logger, ConfigLoader.Settings) { }

    public TooltipWindow(ILogger logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;
        
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        
        _logger.Information("TooltipWindow initialized with fade duration {Duration}ms", 
            settings.TooltipFadeDurationMs);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Make window non-activatable (no focus steal)
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    public void SetCurrentArchivePath(string path)
    {
        _currentArchivePath = path;
    }

    public void ShowLoading(string archiveName, double x, double y)
    {
        ArchiveNameText.Text = $"📦 {archiveName}";
        FileList.ItemsSource = null;
        FolderTree.ItemsSource = null;
        LoadingPanel.Visibility = Visibility.Visible;
        ProgressBar.Visibility = Visibility.Visible;
        ImagePreviewPanel.Visibility = Visibility.Collapsed;
        FooterText.Visibility = Visibility.Collapsed;
        
        FileListScroll.Visibility = Visibility.Visible;
        TreeViewScroll.Visibility = Visibility.Collapsed;
        
        PositionAt(x, y);
        
        Visibility = Visibility.Visible;
        RunFadeIn();
        
        _logger.Debug("Showing loading state for {ArchiveName}", archiveName);
    }

    public void ShowEntries(string archiveName, List<ArchiveEntry> entries, double x, double y)
    {
        ShowEntries(archiveName, entries, x, y, null);
    }

    public void ShowEntries(string archiveName, List<ArchiveEntry> entries, double x, double y, string? archivePath)
    {
        ArchiveNameText.Text = $"📦 {archiveName}";
        LoadingPanel.Visibility = Visibility.Collapsed;
        ProgressBar.Visibility = Visibility.Collapsed;
        ImagePreviewPanel.Visibility = Visibility.Collapsed;

        _currentArchivePath = archivePath;

        int totalCount = entries.Count;
        
        // Check for single image preview
        if (_settings.EnableImagePreview && entries.Count == 1 && IsImageFile(entries[0].Name))
        {
            ShowImagePreview(entries[0], archivePath);
            return;
        }

        var displayEntries = entries.Take(MaxDisplayEntries).ToList();

        if (_settings.EnableTreeView)
        {
            ShowTreeView(displayEntries, totalCount);
        }
        else
        {
            ShowFlatList(displayEntries, totalCount);
        }

        PositionAt(x, y);
        
        Visibility = Visibility.Visible;
        RunFadeIn();
        
        _logger.Information("Showing {Count} entries for {ArchiveName}", displayEntries.Count, archiveName);
    }

    private void ShowFlatList(List<ArchiveEntry> entries, int totalCount)
    {
        FileList.ItemsSource = entries;
        FileListScroll.Visibility = Visibility.Visible;
        TreeViewScroll.Visibility = Visibility.Collapsed;

        if (totalCount > MaxDisplayEntries)
        {
            FooterText.Text = $"... и ещё {totalCount - MaxDisplayEntries} элементов";
            FooterText.Visibility = Visibility.Visible;
        }
        else
        {
            FooterText.Text = $"{totalCount} элементов";
            FooterText.Visibility = Visibility.Visible;
        }
    }

    private void ShowTreeView(List<ArchiveEntry> entries, int totalCount)
    {
        var rootNodes = BuildTree(entries);
        FolderTree.ItemsSource = rootNodes;
        FileListScroll.Visibility = Visibility.Collapsed;
        TreeViewScroll.Visibility = Visibility.Visible;

        if (totalCount > MaxDisplayEntries)
        {
            FooterText.Text = $"... и ещё {totalCount - MaxDisplayEntries} элементов";
            FooterText.Visibility = Visibility.Visible;
        }
        else
        {
            FooterText.Text = $"{totalCount} элементов";
            FooterText.Visibility = Visibility.Visible;
        }

        // Expand all nodes
        ExpandAllTreeNodes(FolderTree.ItemsSource);
    }

    private List<TreeNode> BuildTree(List<ArchiveEntry> entries)
    {
        var root = new TreeNode { Name = "root", Children = new List<TreeNode>() };
        var nodeMap = new Dictionary<string, TreeNode>();

        foreach (var entry in entries)
        {
            var parts = entry.Name.Replace('\\', '/').TrimEnd('/').Split('/');
            var currentNode = root;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var key = string.Join("/", parts.Take(i + 1));

                if (!nodeMap.TryGetValue(key, out var node))
                {
                    bool isLastPart = i == parts.Length - 1;
                    node = new TreeNode
                    {
                        Name = part,
                        FullPath = key,
                        IsDirectory = isLastPart ? entry.IsDirectory : true,
                        Size = isLastPart ? entry.Size : 0,
                        Children = new List<TreeNode>()
                    };
                    nodeMap[key] = node;
                    currentNode.Children.Add(node);
                }

                currentNode = node;
            }
        }

        return root.Children;
    }

    private void ExpandAllTreeNodes(object? itemsSource)
    {
        if (itemsSource is IEnumerable<object> items)
        {
            foreach (var item in items)
            {
                if (item is TreeNode node && node.Children.Any())
                {
                    ExpandAllTreeNodes(node.Children);
                }
            }
        }
    }

    private void ShowImagePreview(ArchiveEntry entry, string? archivePath)
    {
        ImagePreviewPanel.Visibility = Visibility.Visible;
        FileListScroll.Visibility = Visibility.Collapsed;
        TreeViewScroll.Visibility = Visibility.Collapsed;
        
        PreviewText.Text = $"{entry.DisplayName}\n{entry.DisplaySize}";
        
        // Note: Actual image extraction would require extracting to temp
        // For now, show placeholder
        PreviewImage.Source = null;
        
        FooterText.Text = "Предпросмотр изображений требует извлечения";
        FooterText.Visibility = Visibility.Visible;
        
        _logger.Debug("Showing image preview placeholder for {FileName}", entry.Name);
    }

    private bool IsImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    public void HideTooltip()
    {
        RunFadeOut();
    }

    public void UpdatePosition(double x, double y)
    {
        PositionAt(x, y);
    }

    private void RunFadeIn()
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(_settings.TooltipFadeDurationMs));
        BeginAnimation(OpacityProperty, animation);
    }

    private void RunFadeOut()
    {
        var animation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(_settings.TooltipFadeDurationMs));
        animation.Completed += (s, e) =>
        {
            Visibility = Visibility.Hidden;
        };
        BeginAnimation(OpacityProperty, animation);
    }

    private void PositionAt(double x, double y)
    {
        // Get DPI scaling
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        double screenX = x * dpiX;
        double screenY = y * dpiY;

        // Get screen bounds
        var screen = System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)x, (int)y));
        var workArea = screen.WorkingArea;

        double waLeft = workArea.Left * dpiX;
        double waTop = workArea.Top * dpiY;
        double waRight = workArea.Right * dpiX;
        double waBottom = workArea.Bottom * dpiY;

        // Ensure the tooltip stays within the screen
        double finalX = screenX;
        double finalY = screenY;

        if (finalX + ActualWidth > waRight)
            finalX = screenX - ActualWidth - 16;
        if (finalY + ActualHeight > waBottom)
            finalY = screenY - ActualHeight - 16;
        if (finalX < waLeft) finalX = waLeft;
        if (finalY < waTop) finalY = waTop;

        Left = finalX;
        Top = finalY;
    }

    // Context menu handlers
    private void OnOpenWith7zClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentArchivePath))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _currentArchivePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                _logger.Information("Opened archive with default handler: {Path}", _currentArchivePath);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to open archive");
            }
        }
    }

    private void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentArchivePath))
        {
            Clipboard.SetText(_currentArchivePath);
            _logger.Information("Copied archive path to clipboard");
        }
    }

    private void OnExtractClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentArchivePath))
        {
            try
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Выберите папку для извлечения",
                    UseDescriptionForTitle = true
                };

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Extract using SharpCompress or 7-Zip
                    // This is a placeholder - full implementation would extract files
                    _logger.Information("Extract to: {Path}", folderDialog.SelectedPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to extract archive");
            }
        }
    }
}

// Tree node for folder grouping
public class TreeNode
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public List<TreeNode> Children { get; set; } = new();
}
