using ArchiveTransparency.Services;

namespace ArchiveTransparency.Tests;

public class ArchiveReaderTests
{
    [Fact]
    public void Parse7zOutput_HandlesEmptyOutput()
    {
        // Arrange
        var output = "";
        var reader = CreateArchiveReader();
        
        // Act
        var method = typeof(ArchiveReader).GetMethod("Parse7zOutput", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(reader, new object[] { output }) as List<Models.ArchiveEntry>;

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void IsArchiveExtension_ValidatesExtensions()
    {
        // Arrange & Act & Assert
        var method = typeof(ExplorerMonitor).GetMethod("IsArchiveExtension",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        Assert.True((bool)(method?.Invoke(null, new object[] { ".zip" })!));
        Assert.True((bool)(method?.Invoke(null, new object[] { ".ZIP" })!));
        Assert.True((bool)(method?.Invoke(null, new object[] { ".rar" })!));
        Assert.True((bool)(method?.Invoke(null, new object[] { ".7z" })!));
        Assert.False((bool)(method?.Invoke(null, new object[] { ".txt" })!));
        Assert.False((bool)(method?.Invoke(null, new object[] { ".exe" })!));
    }

    private ArchiveReader CreateArchiveReader()
    {
        var logger = Serilog.Log.Logger;
        var settings = new Config.Settings();
        var statistics = new StatisticsService();
        return new ArchiveReader(logger, settings, statistics);
    }
}
