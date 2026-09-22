using System.Reflection;

namespace GoBd.Validation.Tests.Documentation;

/// <summary>
/// The licence and notices the tools carry inside themselves are the repository's own files, as
/// they stand, and say what they must.
/// </summary>
public sealed class LicenceTextsTests
{
    private static string RepositoryFile(string name)
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        for (var candidate = new DirectoryInfo(directory); candidate is not null; candidate = candidate.Parent)
        {
            var path = Path.Combine(candidate.FullName, name);
            if (File.Exists(path) && File.Exists(Path.Combine(candidate.FullName, "GoBdReader.slnx")))
            {
                return File.ReadAllText(path);
            }
        }

        throw new FileNotFoundException($"{name} was not found at the repository root.");
    }

    [Fact]
    public void TheLicenceIsMit() =>
        LicenceTexts.Licence.ShouldStartWith("MIT License");

    [Fact]
    public void TheNoticeSaysTheGrammarIsNotCoveredByTheLicence()
    {
        var notice = LicenceTexts.Notice;

        notice.ShouldContain("gdpdu-01-03-2019.dtd");
        notice.ShouldContain("Audicon's work");
        notice.ShouldContain("It is not covered by the MIT");
    }

    [Fact]
    public void TheLicenceIsTheLicenceFileAndNothingElse()
    {
        // Nothing is appended to it: GitHub reads a licence file as MIT only while it is the MIT
        // text alone, which is why what it does not cover lives in NOTICE.
        LicenceTexts.Licence.ShouldBe(RepositoryFile("LICENSE"));
        LicenceTexts.Licence.ShouldNotContain("Audicon");
    }

    [Fact]
    public void TheNoticeIsTheNoticeFile() =>
        LicenceTexts.Notice.ShouldBe(RepositoryFile("NOTICE"));

    [Fact]
    public void TheCopyrightIsTheLicencesOwnLine() =>
        LicenceTexts.Copyright.ShouldBe(
            RepositoryFile("LICENSE").Split('\n').Select(line => line.Trim())
                .First(line => line.StartsWith("Copyright ", StringComparison.Ordinal)));

    [Fact]
    public void TheNoticesAreTheRepositorysFile() =>
        LicenceTexts.ThirdPartyNotices.ShouldBe(RepositoryFile("THIRD-PARTY-NOTICES.txt"));

    [Theory]
    [InlineData(".NET")]
    [InlineData("DuckDB")]
    [InlineData("Apache License")]
    public void TheNoticesNameWhatTheBuildsContain(string component) =>
        LicenceTexts.ThirdPartyNotices.ShouldContain(component);
}
