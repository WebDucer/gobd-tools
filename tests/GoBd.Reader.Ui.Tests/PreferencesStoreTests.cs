using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Tests;

public sealed class PreferencesStoreTests
{
    [Fact]
    public void MissingFileReturnsDefaultPreferences()
    {
        using var preferences = new TemporaryPreferences();

        var prefs = preferences.Store.Load();
        prefs.Theme.ShouldBe(ThemePreference.System);
        prefs.Language.ShouldBe(LanguageResolver.Resolve(null).Language);
    }

    [Fact]
    public void PreferencesRoundTripSuccessfully()
    {
        using var preferences = new TemporaryPreferences();
        var store = preferences.Store;
        store.Save(new UserPreferences(ThemePreference.Dark, ReportLanguage.German));

        File.Exists(store.FilePath).ShouldBeTrue();
        var reloaded = store.Load();
        reloaded.Theme.ShouldBe(ThemePreference.Dark);
        reloaded.Language.ShouldBe(ReportLanguage.German);
    }

    [Fact]
    public void CorruptedFileFallsBackToDefaultWithoutThrowing()
    {
        using var preferences = new TemporaryPreferences();
        File.WriteAllText(preferences.Store.FilePath, "{ broken json content !!");

        var prefs = preferences.Store.Load();
        prefs.ShouldNotBeNull();
        prefs.Theme.ShouldBe(ThemePreference.System);
        prefs.Language.ShouldBe(LanguageResolver.Resolve(null).Language);
    }

    [Fact]
    public void ChoicesAreWrittenByName()
    {
        using var preferences = new TemporaryPreferences();
        preferences.Store.Save(new UserPreferences(ThemePreference.Dark, ReportLanguage.German));

        var written = File.ReadAllText(preferences.Store.FilePath);
        written.ShouldContain("\"Dark\"");
        written.ShouldContain("\"German\"");
    }

    [Fact]
    public void ChoicesEarlierBuildsWroteAsNumbersAreStillRead()
    {
        using var preferences = new TemporaryPreferences();
        File.WriteAllText(preferences.Store.FilePath, """{"Theme":2,"Language":1}""");

        preferences.Store.Load().ShouldBe(new UserPreferences(ThemePreference.Dark, ReportLanguage.German));
    }

    [Fact]
    public void AChoiceTheReaderDoesNotHaveTakesItsDefaultAndTheOtherIsKept()
    {
        using var preferences = new TemporaryPreferences();
        File.WriteAllText(preferences.Store.FilePath, """{"Theme":5,"Language":"German"}""");

        preferences.Store.Load().ShouldBe(new UserPreferences(ThemePreference.System, ReportLanguage.German));
    }

    [Fact]
    public void ALanguageLeftOutIsTheDetectedOneRatherThanEnglish()
    {
        using var preferences = new TemporaryPreferences();
        File.WriteAllText(preferences.Store.FilePath, """{"Theme":"Dark"}""");

        preferences.Store.Load().ShouldBe(new UserPreferences(ThemePreference.Dark, LanguageResolver.Resolve(null).Language));
    }

    [Fact]
    public void AnAppearanceALaterVersionAddedKeepsTheLanguage()
    {
        // A name this reader does not know is the one choice it costs: the language stays.
        using var preferences = new TemporaryPreferences();
        File.WriteAllText(preferences.Store.FilePath, """{"Theme":"Sepia","Language":"German","Zoom":150}""");

        preferences.Store.Load().ShouldBe(new UserPreferences(ThemePreference.System, ReportLanguage.German, Zoom: 150));
    }

    [Fact]
    public void AChoiceInAFormNoBuildWroteTakesItsDefaultAlone()
    {
        using var preferences = new TemporaryPreferences();
        File.WriteAllText(preferences.Store.FilePath, """{"Theme":"","Language":"2","Zoom":"big","NavigatorCollapsed":"yes","NavigatorWidth":420}""");

        preferences.Store.Load().ShouldBe(new UserPreferences(
            ThemePreference.System,
            LanguageResolver.Resolve(null).Language,
            NavigatorWidth: 420));
    }

    [Fact]
    public void TheNewChoicesRoundTrip()
    {
        using var preferences = new TemporaryPreferences();
        var chosen = new UserPreferences(ThemePreference.HighContrastLight, ReportLanguage.German, 175, 410.5, NavigatorCollapsed: true);
        preferences.Store.Save(chosen);

        preferences.Store.Load().ShouldBe(chosen);
        File.ReadAllText(preferences.Store.FilePath).ShouldContain("\"HighContrastLight\"");
    }

    [Fact]
    public void ZoomAndWidthAreKeptWithinWhatTheReaderOffers()
    {
        using var preferences = new TemporaryPreferences();
        File.WriteAllText(preferences.Store.FilePath, """{"Zoom":900,"NavigatorWidth":12}""");
        var loaded = preferences.Store.Load();
        loaded.Zoom.ShouldBe(UserPreferences.LargestZoom);
        loaded.NavigatorWidth.ShouldBe(UserPreferences.NarrowestNavigator);

        File.WriteAllText(preferences.Store.FilePath, """{"Zoom":40,"NavigatorWidth":99999}""");
        loaded = preferences.Store.Load();
        loaded.Zoom.ShouldBe(UserPreferences.ActualSize);
        loaded.NavigatorWidth.ShouldBe(UserPreferences.WidestNavigator);
    }

    [Fact]
    public void AFileOfAnotherShapeGivesTheDefaults()
    {
        using var preferences = new TemporaryPreferences();
        File.WriteAllText(preferences.Store.FilePath, "[1,2,3]");

        preferences.Store.Load().ShouldBe(UserPreferences.Default);
    }

    [Fact]
    public void DefaultPathIsPlatformAppropriate()
    {
        var path = PreferencesStore.ResolveDefaultPath();
        path.ShouldNotBeNullOrWhiteSpace();
        path.EndsWith("preferences.json", StringComparison.Ordinal).ShouldBeTrue();
    }
}
