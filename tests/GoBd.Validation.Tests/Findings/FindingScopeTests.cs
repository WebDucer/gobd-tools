using GoBd.Validation;
using GoBd.Validation.Findings;
using GoBd.Validation.Tests.Fixtures;

namespace GoBd.Validation.Tests.Findings;

/// <summary>
/// The reported scope is a published JSON field. It used to be recovered by indexing into a
/// finding's positional arguments, so reordering a check's arguments to improve its wording
/// silently changed it and nothing noticed. These hold the replacement honest.
/// </summary>
public sealed class FindingScopeTests
{
    /// <summary>
    /// Codes that legitimately concern the export as a whole rather than a thing within it.
    /// Everything else must name what it is about.
    /// </summary>
    private static readonly HashSet<string> Unscoped = new(StringComparer.Ordinal)
    {
        FindingCodes.IndexMissing, FindingCodes.IndexNotAtRoot,
        FindingCodes.XmlNotWellFormed, FindingCodes.GrammarViolation, FindingCodes.DtdMissing,
        FindingCodes.DtdLegacySystemIdentifier, FindingCodes.ExternalReferenceBlocked,
        FindingCodes.EntityExpansionExceeded,
        FindingCodes.ExportPathNotFound, FindingCodes.ExportUnreadable,
        FindingCodes.LanguageUnsupported, FindingCodes.CheckFailed,
        FindingCodes.EmbeddedGrammarNotCanonical,
    };

    private static IEnumerable<Finding> AllFindings() =>
        new[] { FixtureLibrary.BrokenExport, FixtureLibrary.RepackagedExport, FixtureLibrary.GoodExport }
            .SelectMany(name => ExportValidator.Validate(FixtureLibrary.FolderPath(name)).Findings);

    [Fact]
    public void TheFixturesActuallyProduceFindings() =>
        // Guard on the guard: an empty set would make every assertion below vacuous.
        AllFindings().ShouldNotBeEmpty();

    [Fact]
    public void EveryFindingAboutSomethingNamesWhatItIsAbout()
    {
        var missing = AllFindings()
            .Where(finding => !Unscoped.Contains(finding.Code) && finding.Scope is null)
            .Select(finding => finding.Code)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        missing.ShouldBeEmpty($"these codes reached a report with no scope: {string.Join(", ", missing)}");
    }

    [Fact]
    public void AFindingAboutTheExportItselfCarriesNoScope() =>
        AllFindings()
            .Where(finding => Unscoped.Contains(finding.Code))
            .ShouldAllBe(finding => finding.Scope == null);

    [Fact]
    public void ScopeSurvivesReorderingAFindingsArguments()
    {
        // The point of the change: scope is stated, not derived from argument order.
        var finding = Finding.Create(FindingCodes.ReferenceDangling, null, "Kunden", "Bestellungen")
            .About(FindingScope.Table("Bestellungen"));

        finding.Scope.ShouldNotBeNull().Name.ShouldBe("Bestellungen");
        finding.Scope.Kind.ShouldBe(FindingScopeKind.Table);
        finding.Arguments[0].ShouldBe("Kunden");
    }

    [Fact]
    public void ScopeKindDistinguishesThingsThatShareAName()
    {
        // A medium and a table may both be called "Kunden"; the flat name alone cannot tell them
        // apart, which is why the kind is carried alongside it.
        FindingScope.Medium("Kunden").ShouldNotBe(FindingScope.Table("Kunden"));
        FindingScope.Table("Kunden").Kind.ShouldBe(FindingScopeKind.Table);
        FindingScope.Dtd("gdpdu-01-03-2019.dtd").Kind.ShouldBe(FindingScopeKind.Dtd);
    }
}
