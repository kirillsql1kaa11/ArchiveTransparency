using ArchiveTransparency.Services;

namespace ArchiveTransparency.Tests;

public class StatisticsServiceTests
{
    [Fact]
    public void InitialValues_AreZero()
    {
        // Arrange
        var stats = new StatisticsService();

        // Act & Assert
        Assert.Equal(0, stats.ArchivesProcessed);
        Assert.Equal(0, stats.CacheHits);
        Assert.Equal(0, stats.CacheMisses);
        Assert.Equal(0, stats.TotalRequests);
    }

    [Fact]
    public void RecordArchiveProcessed_IncrementsCount()
    {
        // Arrange
        var stats = new StatisticsService();

        // Act
        stats.RecordArchiveProcessed();
        stats.RecordArchiveProcessed();
        stats.RecordArchiveProcessed();

        // Assert
        Assert.Equal(3, stats.ArchivesProcessed);
    }

    [Fact]
    public void RecordCacheHit_IncrementsCount()
    {
        // Arrange
        var stats = new StatisticsService();

        // Act
        stats.RecordCacheHit();
        stats.RecordCacheHit();

        // Assert
        Assert.Equal(2, stats.CacheHits);
    }

    [Fact]
    public void RecordCacheMiss_IncrementsCount()
    {
        // Arrange
        var stats = new StatisticsService();

        // Act
        stats.RecordCacheMiss();

        // Assert
        Assert.Equal(1, stats.CacheMisses);
    }

    [Fact]
    public void TotalRequests_ReturnsSumOfHitsAndMisses()
    {
        // Arrange
        var stats = new StatisticsService();

        // Act
        stats.RecordCacheHit();
        stats.RecordCacheHit();
        stats.RecordCacheMiss();
        stats.RecordCacheMiss();
        stats.RecordCacheMiss();

        // Assert
        Assert.Equal(5, stats.TotalRequests);
        Assert.Equal(2, stats.CacheHits);
        Assert.Equal(3, stats.CacheMisses);
    }

    [Fact]
    public void Reset_ClearsAllCounters()
    {
        // Arrange
        var stats = new StatisticsService();
        stats.RecordArchiveProcessed();
        stats.RecordCacheHit();
        stats.RecordCacheMiss();

        // Act
        stats.Reset();

        // Assert
        Assert.Equal(0, stats.ArchivesProcessed);
        Assert.Equal(0, stats.CacheHits);
        Assert.Equal(0, stats.CacheMisses);
    }

    [Fact]
    public void GetSummary_ReturnsFormattedString()
    {
        // Arrange
        var stats = new StatisticsService();
        stats.RecordArchiveProcessed();
        stats.RecordCacheHit();
        stats.RecordCacheMiss();

        // Act
        var summary = stats.GetSummary();

        // Assert
        Assert.Contains("Обработано: 1", summary);
        Assert.Contains("Кэш: 1/1", summary);
    }

    [Fact]
    public void Methods_AreThreadSafe()
    {
        // Arrange
        var stats = new StatisticsService();
        var tasks = new List<Task>();

        // Act - concurrent operations
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => stats.RecordArchiveProcessed()));
            tasks.Add(Task.Run(() => stats.RecordCacheHit()));
            tasks.Add(Task.Run(() => stats.RecordCacheMiss()));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.Equal(100, stats.ArchivesProcessed);
        Assert.Equal(100, stats.CacheHits);
        Assert.Equal(100, stats.CacheMisses);
    }
}
