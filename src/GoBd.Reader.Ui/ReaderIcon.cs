using Avalonia.Controls;
using Avalonia.Platform;

namespace GoBd.Reader.Ui;

/// <summary>The reader's icon, as the files the reader itself carries.</summary>
/// <remarks>
/// Both are made from <c>Assets/gobd-reader.svg</c> by <c>.github/scripts/make-reader-icons.sh</c>,
/// and never by hand: the drawing is the one file drawn, and each platform's version is set out
/// from it with that platform's margins. The Windows executable carries the <c>.ico</c> a second
/// time, as its own resource, and the macOS bundle an <c>.icns</c>. See the add-reader-icon
/// change's design.md D1 and D5.
/// </remarks>
internal static class ReaderIcon
{
    /// <summary>Every size from 16 to 256 px, for Windows to choose from.</summary>
    public static readonly Uri Ico = new("avares://gobd-reader/Assets/gobd-reader.ico");

    /// <summary>256 px, for Linux.</summary>
    public static readonly Uri Png = new("avares://gobd-reader/Assets/gobd-reader.png");

    /// <summary>The icon for a window on this platform, or none where windows have none.</summary>
    /// <remarks>
    /// Windows gets the <c>.ico</c>, so it takes its small and its big icon from the sizes drawn
    /// for them rather than scaling one down. A window on macOS has no icon of its own: the Dock
    /// shows the bundle's.
    /// </remarks>
    public static WindowIcon? ForWindow()
    {
        if (OperatingSystem.IsMacOS())
        {
            return null;
        }

        using var file = AssetLoader.Open(OperatingSystem.IsWindows() ? Ico : Png);
        return new WindowIcon(file);
    }
}
