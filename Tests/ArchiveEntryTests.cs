using ArchiveTransparency.Models;

namespace ArchiveTransparency.Tests;

public class ArchiveEntryTests
{
    [Fact]
    public void DisplayName_ReturnsFileName()
    {
        // Arrange
        var entry = new ArchiveEntry { Name = "folder/subfolder/file.txt" };

        // Act
        var result = entry.DisplayName;

        // Assert
        Assert.Equal("file.txt", result);
    }

    [Fact]
    public void DisplayName_ReturnsFullNameForRootFile()
    {
        // Arrange
        var entry = new ArchiveEntry { Name = "file.txt" };

        // Act
        var result = entry.DisplayName;

        // Assert
        Assert.Equal("file.txt", result);
    }

    [Fact]
    public void DisplayPath_ReplacesBackslashesWithForward()
    {
        // Arrange
        var entry = new ArchiveEntry { Name = "folder\\subfolder\\file.txt" };

        // Act
        var result = entry.DisplayPath;

        // Assert
        Assert.Equal("folder/subfolder/file.txt", result);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(1024 * 1024, "1 MB")]
    [InlineData(1024 * 1024 * 512, "512 MB")]
    [InlineData(1024L * 1024 * 1024, "1 GB")]
    public void DisplaySize_FormatsCorrectly(long size, string expected)
    {
        // Arrange
        var entry = new ArchiveEntry { Size = size, IsDirectory = false };

        // Act
        var result = entry.DisplaySize;

        // Assert - check that result contains expected unit
        Assert.Contains(expected.Split(' ')[1], result);
    }

    [Fact]
    public void DisplaySize_EmptyForDirectory()
    {
        // Arrange
        var entry = new ArchiveEntry { Size = 1024, IsDirectory = true };

        // Act
        var result = entry.DisplaySize;

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("file.txt", 0)]
    [InlineData("folder/file.txt", 1)]
    [InlineData("folder/subfolder/file.txt", 2)]
    [InlineData("a/b/c/d/file.txt", 4)]
    public void Depth_CalculatesCorrectly(string name, int expectedDepth)
    {
        // Arrange
        var entry = new ArchiveEntry { Name = name };

        // Act
        var result = entry.Depth;

        // Assert
        Assert.Equal(expectedDepth, result);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "  ")]
    [InlineData(2, "    ")]
    [InlineData(3, "      ")]
    public void Indent_CalculatesCorrectly(int depth, string expected)
    {
        // Arrange
        var entry = new ArchiveEntry { Name = depth == 0 ? "file.txt" : string.Join("/", Enumerable.Repeat("folder", depth)) + "/file.txt" };

        // Act
        var result = entry.Indent;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Icon_ReturnsFolderEmojiForDirectory()
    {
        // Arrange
        var entry = new ArchiveEntry { IsDirectory = true };

        // Act
        var result = entry.Icon;

        // Assert
        Assert.Equal("📁", result);
    }

    [Fact]
    public void Icon_ReturnsFileEmojiForFile()
    {
        // Arrange
        var entry = new ArchiveEntry { IsDirectory = false };

        // Act
        var result = entry.Icon;

        // Assert
        Assert.Equal("📄", result);
    }
}
