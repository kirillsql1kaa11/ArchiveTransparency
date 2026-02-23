namespace ArchiveTransparency.Config;

public class Settings
{
    public string SevenZipPath { get; set; } = "";
    public int CacheExpirationMinutes { get; set; } = 5;
    public int MaxEntries { get; set; } = 200;
    public int MaxDisplayEntries { get; set; } = 200;
    public int PollingIntervalMs { get; set; } = 300;
    public int DebounceIntervalMs { get; set; } = 150;
    public int TooltipFadeDurationMs { get; set; } = 150;
    public string HotkeyShow { get; set; } = "Ctrl+Alt+A";
    public bool EnableImagePreview { get; set; } = true;
    public bool EnableTreeView { get; set; } = false;
    public bool ShowStatistics { get; set; } = true;
    public string LogFilePath { get; set; } = "logs\\app.log";
    public string LogLevel { get; set; } = "Information";
}
