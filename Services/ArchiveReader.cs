using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ArchiveTransparency.Models;
using Microsoft.Win32;

namespace ArchiveTransparency.Services;

public class ArchiveReader
{
    private readonly string? _sevenZipPath;
    private readonly Dictionary<string, (List<ArchiveEntry> Entries, DateTime CachedAt)> _cache = new();
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
    private const int MaxEntries = 200;

    public ArchiveReader()
    {
        _sevenZipPath = FindSevenZip();
    }

    public bool IsAvailable => _sevenZipPath != null;

    private static string? FindSevenZip()
    {
        // Common paths
        string[] paths =
        [
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe"
        ];

        foreach (var p in paths)
            if (File.Exists(p)) return p;

        // Registry
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\7-Zip");
            if (key?.GetValue("Path") is string regPath)
            {
                var full = Path.Combine(regPath, "7z.exe");
                if (File.Exists(full)) return full;
            }
        }
        catch { /* ignore */ }

        // PATH env
        try
        {
            var envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in envPath.Split(';'))
            {
                var full = Path.Combine(dir.Trim(), "7z.exe");
                if (File.Exists(full)) return full;
            }
        }
        catch { /* ignore */ }

        return null;
    }

    public async Task<List<ArchiveEntry>> ReadArchiveAsync(string archivePath, CancellationToken ct)
    {
        // Check cache
        if (_cache.TryGetValue(archivePath, out var cached) && DateTime.Now - cached.CachedAt < CacheExpiry)
            return cached.Entries;

        _cache.Remove(archivePath);

        List<ArchiveEntry>? entries = null;

        // Try 7z CLI
        if (_sevenZipPath != null)
            entries = await ReadWith7zAsync(archivePath, ct);

        // Fallback for .zip
        if (entries == null && Path.GetExtension(archivePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            entries = await ReadZipNativeAsync(archivePath, ct);

        entries ??= [new ArchiveEntry { Name = "⚠️ 7-Zip не найден. Установите 7-Zip.", IsDirectory = false }];

        _cache[archivePath] = (entries, DateTime.Now);
        return entries;
    }

    private async Task<List<ArchiveEntry>?> ReadWith7zAsync(string archivePath, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _sevenZipPath!,
                Arguments = $"l \"{archivePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0 ? Parse7zOutput(output) : null;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private List<ArchiveEntry> Parse7zOutput(string output)
    {
        var entries = new List<ArchiveEntry>();
        var lines = output.Split('\n');

        int separatorCount = 0;
        bool inList = false;
        int nameCol = -1, sizeCol = -1, attrCol = -1;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Detect header line
            if (nameCol < 0 && line.Contains("Name") && line.Contains("Attr"))
            {
                nameCol = line.IndexOf("Name");
                sizeCol = line.IndexOf("Size");
                attrCol = line.IndexOf("Attr");
                continue;
            }

            if (line.TrimStart().StartsWith("---"))
            {
                separatorCount++;
                if (separatorCount == 1) inList = true;
                if (separatorCount >= 2) break;
                continue;
            }

            if (!inList || nameCol < 0 || line.Length <= nameCol) continue;

            string name = line.Substring(nameCol).Trim();
            if (string.IsNullOrEmpty(name)) continue;

            bool isDir = false;
            if (attrCol >= 0 && line.Length > attrCol)
            {
                int attrLen = Math.Min(5, line.Length - attrCol);
                isDir = line.Substring(attrCol, attrLen).Contains('D');
            }

            long size = 0;
            if (sizeCol >= 0 && line.Length > sizeCol)
            {
                var match = Regex.Match(line.Substring(sizeCol).TrimStart(), @"^(\d+)");
                if (match.Success) long.TryParse(match.Groups[1].Value, out size);
            }

            entries.Add(new ArchiveEntry { Name = name, Size = size, IsDirectory = isDir });
            if (entries.Count >= MaxEntries) break;
        }

        return entries;
    }

    private static async Task<List<ArchiveEntry>> ReadZipNativeAsync(string archivePath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var entries = new List<ArchiveEntry>();
            using var zip = ZipFile.OpenRead(archivePath);

            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();
                bool isDir = string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/');
                entries.Add(new ArchiveEntry
                {
                    Name = entry.FullName,
                    Size = entry.Length,
                    IsDirectory = isDir
                });
                if (entries.Count >= MaxEntries) break;
            }

            return entries;
        }, ct);
    }
}
