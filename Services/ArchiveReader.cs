using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ArchiveTransparency.Config;
using ArchiveTransparency.Models;
using Microsoft.Win32;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace ArchiveTransparency.Services;

public class ArchiveReader
{
    private readonly ILogger _logger;
    private readonly Settings _settings;
    private readonly StatisticsService _statistics;
    private readonly string? _sevenZipPath;
    private readonly Dictionary<string, (List<ArchiveEntry> Entries, DateTime CachedAt)> _cache = new();
    private readonly TimeSpan _cacheExpiry;
    private readonly int _maxEntries;

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    public ArchiveReader(ILogger logger, Settings settings, StatisticsService statistics)
    {
        _logger = logger;
        _settings = settings;
        _statistics = statistics;
        _sevenZipPath = FindSevenZip(settings.SevenZipPath);
        _cacheExpiry = TimeSpan.FromMinutes(settings.CacheExpirationMinutes);
        _maxEntries = settings.MaxEntries;

        _logger.Information("ArchiveReader initialized. 7-Zip path: {SevenZipPath}", _sevenZipPath ?? "null");
    }

    public bool IsAvailable => _sevenZipPath != null;

    public bool HasImagePreviewSupport => _settings.EnableImagePreview;

    private static string? FindSevenZip(string? configPath)
    {
        // Check config path first
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            return configPath;

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
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read 7-Zip path from registry");
        }

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
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to find 7-Zip in PATH");
        }

        return null;
    }

    public async Task<List<ArchiveEntry>> ReadArchiveAsync(string archivePath, CancellationToken ct)
    {
        _logger.Debug("Reading archive: {ArchivePath}", archivePath);

        // Check cache
        if (_cache.TryGetValue(archivePath, out var cached) && DateTime.Now - cached.CachedAt < _cacheExpiry)
        {
            _logger.Debug("Cache hit for {ArchivePath}", archivePath);
            _statistics.RecordCacheHit();
            return cached.Entries;
        }

        _cache.Remove(archivePath);
        _statistics.RecordCacheMiss();

        List<ArchiveEntry>? entries = null;

        try
        {
            // Check if file is accessible
            if (!IsFileAccessible(archivePath))
            {
                _logger.Warning("Archive file is not accessible: {ArchivePath}", archivePath);
                return [new ArchiveEntry { Name = "⚠️ Файл заблокирован другим процессом", IsDirectory = false }];
            }

            // Try SharpCompress first (supports many formats natively)
            entries = await ReadWithSharpCompressAsync(archivePath, ct);

            // Try 7z CLI
            if (entries == null && _sevenZipPath != null)
                entries = await ReadWith7zAsync(archivePath, ct);

            // Fallback for .zip
            if (entries == null && Path.GetExtension(archivePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                entries = await ReadZipNativeAsync(archivePath, ct);

            entries ??= [new ArchiveEntry { Name = "⚠️ 7-Zip не найден. Установите 7-Zip.", IsDirectory = false }];

            _cache[archivePath] = (entries, DateTime.Now);
            _statistics.RecordArchiveProcessed();

            _logger.Information("Read {EntryCount} entries from {ArchivePath}", entries.Count, archivePath);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Read operation cancelled for {ArchivePath}", archivePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read archive {ArchivePath}", archivePath);
            entries = [new ArchiveEntry { Name = $"⚠️ Ошибка: {ex.Message}", IsDirectory = false }];
        }

        return entries;
    }

    private static bool IsFileAccessible(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task<List<ArchiveEntry>?> ReadWithSharpCompressAsync(string archivePath, CancellationToken ct)
    {
        try
        {
            return await Task.Run(() =>
            {
                var entries = new List<ArchiveEntry>();

                using var stream = File.OpenRead(archivePath);
                using var archive = ArchiveFactory.Open(stream);

                foreach (var entry in archive.Entries)
                {
                    ct.ThrowIfCancellationRequested();

                    if (entry.IsDirectory)
                    {
                        entries.Add(new ArchiveEntry
                        {
                            Name = entry.Key ?? "",
                            Size = entry.Size,
                            IsDirectory = true
                        });
                    }
                    else if (!entry.IsDirectory)
                    {
                        entries.Add(new ArchiveEntry
                        {
                            Name = entry.Key ?? "",
                            Size = entry.Size,
                            IsDirectory = false
                        });
                    }

                    if (entries.Count >= _maxEntries) break;
                }

                return entries.Count > 0 ? entries : null;
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.Debug(ex, "SharpCompress failed for {ArchivePath}", archivePath);
            return null;
        }
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

            _logger.Debug("7z exit code: {ExitCode}", process.ExitCode);

            return process.ExitCode == 0 ? Parse7zOutput(output) : null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.Warning(ex, "7z CLI failed for {ArchivePath}", archivePath);
            return null;
        }
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
            if (entries.Count >= _maxEntries) break;
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
                if (entries.Count >= 200) break;
            }

            return entries;
        }, ct);
    }

    public bool IsImageFile(string fileName)
    {
        if (!_settings.EnableImagePreview) return false;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ImageExtensions.Contains(ext);
    }
}
