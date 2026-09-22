using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GoBd.Validation;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// What the reader says about itself from its Help menu, and the window it says it in.
/// </summary>
/// <remarks>
/// The entries are chosen from the menu the window sets, the way the menu bar chooses them, so a
/// test keeps passing only while a person could still find them there.
/// </remarks>
public sealed class HelpMenuTests : HeadlessTest
{
    private const string Tables = """
                <Table>
                  <URL>t.csv</URL>
                  <Name>T</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Id</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                  </VariableLength>
                </Table>
        """;

    private static IReadOnlyList<NativeMenuItem> Items(NativeMenu menu) => [.. menu.Items.OfType<NativeMenuItem>()];

    private static NativeMenu Help(MainWindow window) =>
        Items(NativeMenu.GetMenu(window).ShouldNotBeNull()).Single(item => (string?)item.Header == "Help").Menu.ShouldNotBeNull();

    /// <summary>The application's own menu, which macOS shows under the application's name.</summary>
    private static NativeMenu? ApplicationMenu() =>
        Application.Current is { } application ? NativeMenu.GetMenu(application) : null;

    /// <summary>
    /// Chooses an entry, from the Help menu or from the application menu, and returns the window
    /// it opened or brought forward.
    /// </summary>
    private static Window Choose(MainWindow window, string entry)
    {
        var offered = Items(Help(window)).Concat(ApplicationMenu() is { } application ? Items(application) : []);
        var item = offered.Single(candidate => (string?)candidate.Header == entry);
        item.Command.ShouldNotBeNull().Execute(null);
        return window.OwnedWindows.Single(owned => owned.Title == entry);
    }

    /// <summary>The same, for an entry that shows a text.</summary>
    private static TextWindow ChooseText(MainWindow window, string entry) =>
        Choose(window, entry).ShouldBeOfType<TextWindow>();

    private static void WithWindow(MainWindow window, Action<MainWindow> body)
    {
        window.Show();
        try
        {
            body(window);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public Task AShortTextIsOneBlockThatCanBeSelected() => Ui(() =>
    {
        var text = string.Join('\n', Enumerable.Range(1, 50).Select(line => $"Line {line}"));
        var window = new TextWindow("Short", text);
        window.Show();
        try
        {
            window.UpdateLayout();

            window.Lines.ShouldBeNull();
            window.Body.ShouldNotBeNull().Text.ShouldBe(text);
            window.Scroller.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task ALongTextIsALineAtATimeAndCanBeReadToItsLastLine() => Ui(() =>
    {
        // As one block, the notices laid out every one of their thousands of lines on every pass,
        // and the window dragged. The list realises the lines it shows.
        var count = TextWindow.LinesBeyondWhichItIsAList * 5;
        var text = string.Join('\n', Enumerable.Range(1, count).Select(line => $"Line {line}"));
        var window = new TextWindow("Long", text);
        window.Show();
        try
        {
            window.UpdateLayout();
            var list = window.Lines.ShouldNotBeNull();
            list.ItemCount.ShouldBe(count);

            var realised = list.GetRealizedContainers().Count();
            realised.ShouldBeLessThan(count / 4, "the lines are realised as they are shown");
            realised.ShouldBeGreaterThan(0);

            list.ScrollIntoView(count - 1);
            window.UpdateLayout();

            list.GetRealizedContainers()
                .Select(container => container.DataContext)
                .ShouldContain($"Line {count}");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task CopyingFromALongTextTakesTheSelectedLinesOrAllOfThem() => Ui(() =>
    {
        var count = TextWindow.LinesBeyondWhichItIsAList * 2;
        var text = string.Join('\n', Enumerable.Range(1, count).Select(line => $"Line {line}"));
        var window = new TextWindow("Long", text);
        window.Show();
        try
        {
            window.Selection().ShouldBe(text);

            window.Lines.ShouldNotBeNull().SelectedItems!.Add("Line 2");
            window.Lines.SelectedItems!.Add("Line 3");

            window.Selection().ShouldBe("Line 2" + Environment.NewLine + "Line 3");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task TheHelpMenuFollowsFileAndOffersTheThreeTexts() => Ui(() => WithWindow(new MainWindow(null), window =>
    {
        Items(NativeMenu.GetMenu(window).ShouldNotBeNull()).Select(item => (string?)item.Header)
            .ShouldBe(["File", "Help"]);

        var offered = Items(Help(window)).Select(item => (string?)item.Header).ToArray();
        offered.ShouldBe(OperatingSystem.IsMacOS()
            ? [MainWindow.LicenceTitle, MainWindow.NoticeTitle, MainWindow.NoticesTitle]
            : [MainWindow.LicenceTitle, MainWindow.NoticeTitle, MainWindow.NoticesTitle, MainWindow.AboutTitle]);
    }));

    [Fact]
    public Task AboutSitsWhereThePlatformExpectsIt() => Ui(() => WithWindow(new MainWindow(null), window =>
    {
        // On macOS it belongs in the menu named after the application, where the toolkit would
        // otherwise leave its own "About Avalonia"; everywhere else there is no such menu.
        var application = ApplicationMenu();
        if (OperatingSystem.IsMacOS())
        {
            Items(application.ShouldNotBeNull()).Select(item => (string?)item.Header)
                .ShouldContain(MainWindow.AboutTitle);
        }
        else
        {
            (application is null || Items(application).All(item => (string?)item.Header != MainWindow.AboutTitle))
                .ShouldBeTrue("only macOS has an application menu");
            Items(Help(window)).Select(item => (string?)item.Header).ShouldContain(MainWindow.AboutTitle);
        }
    }));

    [Fact]
    public Task TheLicenceIsShownOnItsOwn() => Ui(() => WithWindow(new MainWindow(null), window =>
    {
        // The licence and what it does not cover are apart, as LICENSE and NOTICE are apart.
        var licence = ChooseText(window, MainWindow.LicenceTitle);

        licence.Text.ShouldBe(LicenceTexts.Licence);
        licence.Text.ShouldNotContain("Audicon");
    }));

    [Fact]
    public Task WhatTheLicenceDoesNotCoverIsShown() => Ui(() => WithWindow(new MainWindow(null), window =>
        ChooseText(window, MainWindow.NoticeTitle).Text.ShouldBe(LicenceTexts.Notice)));

    [Fact]
    public Task TheNoticesAreShown() => Ui(() => WithWindow(new MainWindow(null), window =>
        ChooseText(window, MainWindow.NoticesTitle).Text.ShouldBe(LicenceTexts.ThirdPartyNotices)));

    [Fact]
    public Task AboutShowsTheReaderRatherThanAParagraph() => Ui(() => WithWindow(new MainWindow(null), window =>
    {
        var about = Choose(window, MainWindow.AboutTitle).ShouldBeOfType<AboutWindow>();
        about.UpdateLayout();
        var texts = Driver.Texts(about);

        texts.ShouldContain("GoBD Reader");
        texts.ShouldContain(AboutWindow.VersionText);
        texts.ShouldContain(text => text.Contains("MIT licence", StringComparison.Ordinal)
            && text.Contains(LicenceTexts.Copyright.Replace("(c)", "©", StringComparison.Ordinal), StringComparison.Ordinal));
        about.GetLogicalDescendants().OfType<Image>().ShouldNotBeEmpty("the icon is shown");
        about.GetLogicalDescendants().OfType<HyperlinkButton>()
            .ShouldContain(link => link.NavigateUri == new Uri(AboutWindow.ProjectUrl));
    }));

    [Theory]
    [InlineData(MainWindow.LicenceTitle)]
    [InlineData(MainWindow.NoticeTitle)]
    [InlineData(MainWindow.NoticesTitle)]
    public Task AboutOpensEachTextItOffers(string entry) => Ui(() => WithWindow(new MainWindow(null), window =>
    {
        var about = Choose(window, MainWindow.AboutTitle).ShouldBeOfType<AboutWindow>();

        var button = about.GetLogicalDescendants().OfType<Button>().Single(candidate => (string?)candidate.Content == entry);
        Driver.Click(button);

        window.OwnedWindows.OfType<TextWindow>().ShouldContain(text => text.Title == entry);
    }));

    [Fact]
    public Task ChoosingAnEntryAgainBringsItsWindowForward() => Ui(() => WithWindow(new MainWindow(null), window =>
    {
        var first = ChooseText(window, MainWindow.NoticesTitle);
        var second = ChooseText(window, MainWindow.NoticesTitle);

        second.ShouldBeSameAs(first);
        window.OwnedWindows.OfType<TextWindow>().Count(owned => owned.Title == MainWindow.NoticesTitle).ShouldBe(1);
    }));

    [Fact]
    public Task AClosedWindowIsOpenedAfresh() => Ui(() => WithWindow(new MainWindow(null), window =>
    {
        var first = ChooseText(window, MainWindow.LicenceTitle);
        first.Close();

        ChooseText(window, MainWindow.LicenceTitle).ShouldNotBeSameAs(first);
    }));

    [Fact]
    public Task AnEntryIsShownWhileAnExportIsReadAndTheReadingCompletes() => Ui(() =>
    {
        using var harness = ExportHarness.Create(Tables, ("t.csv", "A;10,00\r\nB;20,00\r\n"));
        WithWindow(new MainWindow(harness.ExportPath, harness.Options), window =>
        {
            var reading = window.Reading;

            ChooseText(window, MainWindow.NoticesTitle).Text.ShouldBe(LicenceTexts.ThirdPartyNotices);

            // The reading reports to this thread, so the dispatcher is kept running while it is
            // waited for, as the window's own loop would.
            var clock = Stopwatch.StartNew();
            while (!reading.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(60))
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }

            reading.IsCompletedSuccessfully.ShouldBeTrue();
            window.Session.ShouldNotBeNull();
        });
    });
}
