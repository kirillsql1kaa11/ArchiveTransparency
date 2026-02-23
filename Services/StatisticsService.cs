namespace ArchiveTransparency.Services;

public class StatisticsService
{
    private int _archivesProcessed;
    private int _cacheHits;
    private int _cacheMisses;
    private readonly object _lock = new();

    public int ArchivesProcessed => _archivesProcessed;
    public int CacheHits => _cacheHits;
    public int CacheMisses => _cacheMisses;
    public int TotalRequests => _cacheHits + _cacheMisses;

    public void RecordArchiveProcessed()
    {
        lock (_lock)
        {
            _archivesProcessed++;
        }
    }

    public void RecordCacheHit()
    {
        lock (_lock)
        {
            _cacheHits++;
        }
    }

    public void RecordCacheMiss()
    {
        lock (_lock)
        {
            _cacheMisses++;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _archivesProcessed = 0;
            _cacheHits = 0;
            _cacheMisses = 0;
        }
    }

    public string GetSummary()
    {
        lock (_lock)
        {
            return $"Обработано: {_archivesProcessed} | Кэш: {_cacheHits}/{_cacheMisses}";
        }
    }
}
