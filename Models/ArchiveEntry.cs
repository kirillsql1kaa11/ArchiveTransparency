namespace ArchiveTransparency.Models;

public class ArchiveEntry
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public bool IsDirectory { get; set; }

    public string Icon => IsDirectory ? "📁" : "📄";

    public string DisplayName
    {
        get
        {
            // Show only the last segment of the path
            var parts = Name.Replace('\\', '/').TrimEnd('/').Split('/');
            return parts.Length > 0 ? parts[^1] : Name;
        }
    }

    public string DisplayPath => Name.Replace('\\', '/');

    public string DisplaySize
    {
        get
        {
            if (IsDirectory) return "";
            if (Size < 1024) return $"{Size} B";
            if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
            if (Size < 1024L * 1024 * 1024) return $"{Size / (1024.0 * 1024):F1} MB";
            return $"{Size / (1024.0 * 1024 * 1024):F1} GB";
        }
    }

    public int Depth => Name.Replace('\\', '/').TrimEnd('/').Split('/').Length - 1;

    public string Indent => new string(' ', Depth * 2);
}
