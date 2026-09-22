using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Tests.Localisation;

public sealed class ReportLanguagesTests
{
    [Theory]
    [InlineData("de", ReportLanguage.German)]
    [InlineData("de-DE", ReportLanguage.German)]
    [InlineData("de_DE.UTF-8", ReportLanguage.German)]
    [InlineData("DE", ReportLanguage.German)]
    [InlineData("de_AT", ReportLanguage.German)]
    [InlineData("en", ReportLanguage.English)]
    [InlineData("en-GB", ReportLanguage.English)]
    [InlineData("en_US.UTF-8", ReportLanguage.English)]
    public void SupportedTagsAreRecognised(string tag, ReportLanguage expected) =>
        ReportLanguages.Parse(tag).ShouldBe(expected);

    [Theory]
    [InlineData("fr")] [InlineData("fr_FR.UTF-8")] [InlineData("C")] [InlineData("POSIX")]
    [InlineData("")] [InlineData("   ")] [InlineData(null)]
    public void UnsupportedTagsYieldNothing(string? tag) => ReportLanguages.Parse(tag).ShouldBeNull();

    [Theory]
    [InlineData("de_DE.UTF-8", "de")]
    [InlineData("en-GB", "en")]
    [InlineData("de@euro", "de")]
    [InlineData("fr", "fr")]
    public void PrimarySubtagIsExtractedWithoutCultureInfo(string tag, string expected) =>
        // CultureInfo is unavailable in globalization-invariant mode, so this is done by hand.
        ReportLanguages.PrimarySubtag(tag).ToString().ShouldBe(expected);
}

public sealed class LanguageResolverTests
{
    private sealed class FakeEnvironment(string? gobdLang = null, string? osLanguage = null) : ILanguageEnvironment
    {
        public string? GetEnvironmentVariable(string name) =>
            string.Equals(name, LanguageResolver.EnvironmentVariable, StringComparison.Ordinal) ? gobdLang : null;

        public string? GetOperatingSystemLanguage() => osLanguage;
    }

    [Fact]
    public void ExplicitRequestOverridesEverything()
    {
        var resolution = LanguageResolver.Resolve("en", new FakeEnvironment(gobdLang: "de", osLanguage: "de_DE.UTF-8"));

        resolution.Language.ShouldBe(ReportLanguage.English);
        resolution.Finding.ShouldBeNull();
    }

    [Fact]
    public void EnvironmentVariableWinsOverTheOperatingSystem() =>
        LanguageResolver.Resolve(null, new FakeEnvironment(gobdLang: "de", osLanguage: "en_US.UTF-8"))
            .Language.ShouldBe(ReportLanguage.German);

    [Fact]
    public void GermanOperatingSystemYieldsGerman() =>
        LanguageResolver.Resolve(null, new FakeEnvironment(osLanguage: "de_DE.UTF-8"))
            .Language.ShouldBe(ReportLanguage.German);

    [Fact]
    public void NonGermanOperatingSystemYieldsEnglish() =>
        LanguageResolver.Resolve(null, new FakeEnvironment(osLanguage: "fr_FR.UTF-8"))
            .Language.ShouldBe(ReportLanguage.English);

    [Fact]
    public void UndeterminableOperatingSystemLanguageFallsBackToEnglish()
    {
        var resolution = LanguageResolver.Resolve(null, new FakeEnvironment());

        resolution.Language.ShouldBe(ReportLanguage.English);
        resolution.Finding.ShouldBeNull();
    }

    [Fact]
    public void UnsupportedRequestFallsBackToEnglishAndSaysSo()
    {
        var resolution = LanguageResolver.Resolve("fr", new FakeEnvironment(osLanguage: "de_DE.UTF-8"));

        // Refusing to produce a report over a language tag would be a poor trade.
        resolution.Language.ShouldBe(ReportLanguage.English);
        resolution.Finding.ShouldNotBeNull().Code.ShouldBe(FindingCodes.LanguageUnsupported);
        resolution.Finding.Severity.ShouldBe(Severity.Info);
        resolution.Finding.Arguments.ShouldContain("fr");
    }

    [Fact]
    public void UnsupportedEnvironmentVariableFallsThroughToTheOperatingSystem() =>
        // Only an explicit request is worth reporting; a stale variable just does not match.
        LanguageResolver.Resolve(null, new FakeEnvironment(gobdLang: "fr", osLanguage: "de_DE.UTF-8"))
            .Language.ShouldBe(ReportLanguage.German);

    [Fact]
    public void RealEnvironmentResolutionDoesNotThrowInInvariantMode() =>
        // CultureInfo.GetCultureInfo("de-DE") would throw here; resolution must not use it.
        Should.NotThrow(() => LanguageResolver.Resolve(null));

    [Fact]
    public void SystemEnvironmentReadsThePosixLocaleVariables()
    {
        var environment = new SystemLanguageEnvironment();
        var original = Environment.GetEnvironmentVariable("LC_ALL");
        try
        {
            Environment.SetEnvironmentVariable("LC_ALL", "de_DE.UTF-8");

            if (OperatingSystem.IsWindows())
            {
                // Windows uses the NLS call, not the POSIX variables.
                environment.GetOperatingSystemLanguage().ShouldNotBeNull();
            }
            else
            {
                environment.GetOperatingSystemLanguage().ShouldBe("de_DE.UTF-8");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("LC_ALL", original);
        }
    }
}
