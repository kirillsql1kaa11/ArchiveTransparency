using ArchiveTransparency.Services;

namespace ArchiveTransparency.Tests;

public class HotkeyServiceTests
{
    [Theory]
    [InlineData("Ctrl+A", HotkeyService.ModifierKeys.Control, HotkeyService.Keys.A)]
    [InlineData("Ctrl+Alt+A", HotkeyService.ModifierKeys.Control | HotkeyService.ModifierKeys.Alt, HotkeyService.Keys.A)]
    [InlineData("Ctrl+Shift+A", HotkeyService.ModifierKeys.Control | HotkeyService.ModifierKeys.Shift, HotkeyService.Keys.A)]
    [InlineData("Ctrl+Alt+Shift+A", HotkeyService.ModifierKeys.Control | HotkeyService.ModifierKeys.Alt | HotkeyService.ModifierKeys.Shift, HotkeyService.Keys.A)]
    [InlineData("Alt+F4", HotkeyService.ModifierKeys.Alt, HotkeyService.Keys.F4)]
    [InlineData("ctrl+a", HotkeyService.ModifierKeys.Control, HotkeyService.Keys.A)]
    [InlineData("CTRL+A", HotkeyService.ModifierKeys.Control, HotkeyService.Keys.A)]
    public void TryParseHotkey_ParsesValidHotkeys(string hotkey, HotkeyService.ModifierKeys expectedModifiers, HotkeyService.Keys expectedKey)
    {
        // Act
        var result = HotkeyService.TryParseHotkey(hotkey, out var modifiers, out var key);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedKey, key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Invalid")]
    [InlineData("Ctrl+")]
    public void TryParseHotkey_ReturnsFalseForInvalidHotkeys(string hotkey)
    {
        // Act
        var result = HotkeyService.TryParseHotkey(hotkey, out var modifiers, out var key);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryParseHotkey_HandlesSingleLetter()
    {
        // Act
        var result = HotkeyService.TryParseHotkey("A", out var modifiers, out var key);

        // Assert
        Assert.True(result);
        Assert.Equal(HotkeyService.ModifierKeys.None, modifiers);
        Assert.Equal(HotkeyService.Keys.A, key);
    }

    [Fact]
    public void TryParseHotkey_HandlesLowerCaseLetter()
    {
        // Act
        var result = HotkeyService.TryParseHotkey("z", out var modifiers, out var key);

        // Assert
        Assert.True(result);
        Assert.Equal(HotkeyService.ModifierKeys.None, modifiers);
        Assert.Equal(HotkeyService.Keys.Z, key);
    }
}
