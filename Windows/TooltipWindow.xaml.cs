using System.Windows;
using System.Windows.Interop;
using ArchiveTransparency.Helpers;
using ArchiveTransparency.Models;

namespace ArchiveTransparency.Windows;

public partial class TooltipWindow : Window
{
    private const int MaxDisplayEntries = 200;

    public TooltipWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Make window non-activatable (no focus steal)
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    public void ShowLoading(string archiveName, double x, double y)
    {
        ArchiveNameText.Text = $"📦 {archiveName}";
        FileList.ItemsSource = null;
        LoadingPanel.Visibility = Visibility.Visible;
        FooterText.Visibility = Visibility.Collapsed;
        PositionAt(x, y);
        Visibility = Visibility.Visible;
    }

    public void ShowEntries(string archiveName, List<ArchiveEntry> entries, double x, double y)
    {
        ArchiveNameText.Text = $"📦 {archiveName}";
        LoadingPanel.Visibility = Visibility.Collapsed;

        int totalCount = entries.Count;
        var displayEntries = entries.Take(MaxDisplayEntries).ToList();
        FileList.ItemsSource = displayEntries;

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

        PositionAt(x, y);
        Visibility = Visibility.Visible;
    }

    public void HideTooltip()
    {
        Visibility = Visibility.Hidden;
    }

    public void UpdatePosition(double x, double y)
    {
        PositionAt(x, y);
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
}
