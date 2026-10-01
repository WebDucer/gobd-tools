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
    public void DefaultPathIsPlatformAppropriate()
    {
        var path = PreferencesStore.ResolveDefaultPath();
        path.ShouldNotBeNullOrWhiteSpace();
        path.EndsWith("preferences.json", StringComparison.Ordinal).ShouldBeTrue();
    }
}
