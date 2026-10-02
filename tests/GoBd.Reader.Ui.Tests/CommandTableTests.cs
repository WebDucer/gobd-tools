using Avalonia.Input;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// The command table every menu, shortcut and the shortcut overview are built from.
/// </summary>
/// <remarks>
/// Checked for both platforms whichever the tests run on, because a shortcut bound twice on macOS
/// is a defect on a build these tests never run on. See the improve-reader-accessibility change's
/// design.md D7.
/// </remarks>
public sealed class CommandTableTests
{
    public static TheoryData<bool> Platforms => [true, false];

    [Fact]
    public void OnMacOsTheMenuShowsZoomInAsThePlusKey()
    {
        // The menu bar writes a shortcut as the character it types on a US keyboard: Key.OemPlus
        // is "=" there, and a German keyboard's "+" never matched "⌘=".
        ReaderKeys.ZoomIn.Mac[0].Key.ShouldBe(Key.Add);
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public void NoShortcutMeansTwoThingsInOnePlace(bool mac)
    {
        foreach (var scope in Enum.GetValues<CommandScope>())
        {
            var bound = ReaderCommands.All
                .Where(command => command.Scope == scope)
                .SelectMany(command => Keys(command, mac).Select(gesture => (Gesture: gesture.ToString(), command.Id)))
                .GroupBy(binding => binding.Gesture)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key}: {string.Join(", ", group.Select(binding => binding.Id))}");

            bound.ShouldBeEmpty($"in scope {scope} on {(mac ? "macOS" : "Windows and Linux")}");
        }
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public void TheWindowsShortcutsLeaveTheKeysOfRecordsAndNavigatorAlone(bool mac)
    {
        // The window sees a key before the control with focus does, so a shortcut it answered
        // anywhere would never reach the records or the navigator.
        var everywhere = ReaderCommands.All
            .Where(command => command.Scope == CommandScope.Global)
            .SelectMany(command => Keys(command, mac))
            .Select(gesture => gesture.ToString())
            .ToHashSet(StringComparer.Ordinal);

        var taken = ReaderCommands.All
            .Where(command => command.Scope != CommandScope.Global)
            .SelectMany(command => Keys(command, mac).Select(gesture => (Gesture: gesture.ToString(), command.Id)))
            .Where(binding => everywhere.Contains(binding.Gesture));

        taken.ShouldBeEmpty();
    }

    [Fact]
    public void EveryCommandThatDoesSomethingStandsInAMenu()
    {
        // Keys that only move — between areas, to a tab by its place — have no entry; everything
        // that does something can be found in a menu.
        string[] moving = [ReaderCommands.NextArea, ReaderCommands.PreviousArea, .. Enumerable.Range(1, 9).Select(ReaderCommands.TabAt)];

        ReaderCommands.All
            .Where(command => command.Scope == CommandScope.Global && !moving.Contains(command.Id))
            .Where(command => command.Menu is null)
            .Select(command => command.Id)
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ReportLanguage.English)]
    [InlineData(ReportLanguage.German)]
    public void EveryEntryHasALabelAndAnAccessKeyOfItsOwnInItsMenu(ReportLanguage language)
    {
        foreach (var command in ReaderCommands.All)
        {
            command.Label(language).ShouldNotBeNullOrWhiteSpace(command.Id);
        }

        foreach (var (menu, label, access) in ReaderCommands.Menus)
        {
            var key = language == ReportLanguage.German ? access.German : access.English;
            label(language).ShouldContain(key.ToString(), Case.Insensitive, $"menu {menu}");

            var entries = ReaderCommands.All.Where(command => command.Menu == menu).ToArray();
            foreach (var entry in entries)
            {
                entry.Label(language).ShouldContain(entry.AccessKeyIn(language).ToString(), Case.Insensitive, entry.Id);
            }

            entries.Select(entry => char.ToUpperInvariant(entry.AccessKeyIn(language)))
                .ShouldBeUnique($"access keys of {menu} in {language}");
        }

        ReaderCommands.Menus.Select(menu => char.ToUpperInvariant(language == ReportLanguage.German ? menu.AccessKey.German : menu.AccessKey.English))
            .ShouldBeUnique($"access keys of the menu bar in {language}");
    }

    [Fact]
    public void AnAccessKeyIsMarkedWhereThePlatformShowsOne()
    {
        ReaderKeys.MenuTitle("Open Archive…", 'A', mac: false).ShouldBe("Open _Archive…");
        ReaderKeys.MenuTitle("Zoom Out", 'U', mac: false).ShouldBe("Zoom O_ut");
        ReaderKeys.MenuTitle("Open Archive…", 'A', mac: true).ShouldBe("Open Archive…");
    }

    [Fact]
    public void AShortcutIsWrittenAsThePlatformAndTheLanguageWriteIt()
    {
        var back = new KeyGesture(Key.Left, KeyModifiers.Alt);
        var folder = new KeyGesture(Key.O, KeyModifiers.Control | KeyModifiers.Shift);

        ReaderKeys.Describe(folder, ReportLanguage.English, mac: false).ShouldBe("Ctrl+Shift+O");
        ReaderKeys.Describe(folder, ReportLanguage.German, mac: false).ShouldBe("Strg+Umschalt+O");
        ReaderKeys.Describe(back, ReportLanguage.German, mac: false).ShouldBe("Alt+Pfeil links");
        ReaderKeys.Describe(new KeyGesture(Key.O, KeyModifiers.Meta | KeyModifiers.Shift), ReportLanguage.English, mac: true).ShouldBe("⇧⌘O");
    }

    /// <summary>
    /// A command's shortcuts as Avalonia tells keys apart: the number pad's plus, minus and point
    /// are the same keys as the others, so a shortcut on each would be one shortcut bound twice.
    /// </summary>
    private static IReadOnlyList<KeyGesture> Keys(CommandDefinition command, bool mac) =>
        [.. (mac ? command.Keys.Mac : command.Keys.Other).Select(gesture => new KeyGesture(gesture.Key switch
        {
            Key.Add => Key.OemPlus,
            Key.Subtract => Key.OemMinus,
            Key.Decimal => Key.OemPeriod,
            _ => gesture.Key,
        }, gesture.KeyModifiers))];
}
