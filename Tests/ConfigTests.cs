using ArchiveTransparency.Config;
using ArchiveTransparency.Windows;

namespace ArchiveTransparency.Tests;

public class ConfigTests
{
    [Fact]
    public void Settings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new Settings();

        // Assert
        Assert.Equal("", settings.SevenZipPath);
        Assert.Equal(5, settings.CacheExpirationMinutes);
        Assert.Equal(200, settings.MaxEntries);
        Assert.Equal(200, settings.MaxDisplayEntries);
        Assert.Equal(300, settings.PollingIntervalMs);
        Assert.Equal(150, settings.DebounceIntervalMs);
        Assert.Equal(150, settings.TooltipFadeDurationMs);
        Assert.Equal("Ctrl+Alt+A", settings.HotkeyShow);
        Assert.True(settings.EnableImagePreview);
        Assert.False(settings.EnableTreeView);
        Assert.True(settings.ShowStatistics);
        Assert.Equal("logs\\app.log", settings.LogFilePath);
        Assert.Equal("Information", settings.LogLevel);
    }
}

public class TreeNodeTests
{
    [Fact]
    public void TreeNode_DefaultConstructor_CreatesEmptyNode()
    {
        // Arrange & Act
        var node = new TreeNode();

        // Assert
        Assert.Equal("", node.Name);
        Assert.Equal("", node.FullPath);
        Assert.False(node.IsDirectory);
        Assert.Equal(0, node.Size);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void TreeNode_Initializer_SetsProperties()
    {
        // Arrange & Act
        var node = new TreeNode
        {
            Name = "test",
            FullPath = "/test",
            IsDirectory = true,
            Size = 1024
        };

        // Assert
        Assert.Equal("test", node.Name);
        Assert.Equal("/test", node.FullPath);
        Assert.True(node.IsDirectory);
        Assert.Equal(1024, node.Size);
        Assert.NotNull(node.Children);
    }
}
