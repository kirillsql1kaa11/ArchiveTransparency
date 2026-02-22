using System.Diagnostics;
using Microsoft.Win32;

namespace ArchiveTransparency.Helpers;

public static class AutoStartHelper
{
    private const string AppName = "ArchiveTransparency";
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }
    }

    public static void Enable()
    {
        try
        {
            string exePath = GetExePath();
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            key?.SetValue(AppName, $"\"{exePath}\"");
        }
        catch { /* ignore */ }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            key?.DeleteValue(AppName, false);
        }
        catch { /* ignore */ }
    }

    public static void Toggle()
    {
        if (IsEnabled)
            Disable();
        else
            Enable();
    }

    private static string GetExePath()
    {
        var process = Process.GetCurrentProcess();
        return process.MainModule?.FileName ?? "";
    }
}
