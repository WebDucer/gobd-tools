using GoBd.Validation.Findings;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Findings;

public sealed class FindingTests
{
    [Fact]
    public void FindingCarriesCodeSeverityLocationAndArguments()
    {
        var finding = Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(42, 7), "Bestellungen", "Kunden");

        finding.Code.ShouldBe(FindingCodes.ReferenceDangling);
        finding.Severity.ShouldBe(Severity.Error);
        finding.Location.ShouldBe(new SourceLocation(42, 7));
        finding.Arguments.ShouldBe(["Bestellungen", "Kunden"]);
    }

    [Fact]
    public void FindingAboutTheExportAsAWholeOmitsItsLocation()
    {
        // A missing index.xml concerns the export, not a position inside a document that does
        // not exist. The location is absent rather than a placeholder such as 0:0.
        var finding = Finding.Create(FindingCodes.IndexMissing, location: null, "/tmp/export.zip");

        finding.Location.ShouldBeNull();
        finding.Severity.ShouldBe(Severity.Error);
    }

    [Fact]
    public void SeverityDefaultsComeFromTheCatalogue()
    {
        Finding.Create(FindingCodes.MediaWithoutTables, null).Severity.ShouldBe(Severity.Warning);
        Finding.Create(FindingCodes.DtdLegacySystemIdentifier, null).Severity.ShouldBe(Severity.Info);
        Finding.Create(FindingCodes.DtdAltered, null).Severity.ShouldBe(Severity.Error);
    }

    [Fact]
    public void SeverityCanBeEscalatedAwayFromTheCatalogueDefault()
    {
        // A duplicated table name is a warning, unless a ForeignKey actually references it.
        var escalated = Finding.Create(FindingCodes.TableNameDuplicated, Severity.Error, null, "Kunden");

        escalated.Severity.ShouldBe(Severity.Error);
        FindingCodes.SeverityOf(FindingCodes.TableNameDuplicated).ShouldBe(Severity.Warning);
    }

    [Fact]
    public void UnknownCodeIsRejected() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Finding.Create("GOBD0000", null));

    [Fact]
    public void SourceLocationRendersInvariantly() =>
        new SourceLocation(42, 7).ToString().ShouldBe("42:7");
}

/// <summary>
/// Findings are compared and deduplicated, so value equality must hold. These tests exist
/// because the record's generated equality is only as good as its members' equality: an
/// ImmutableArray member would compare by underlying reference and break all of this.
/// </summary>
public sealed class FindingEqualityTests
{
    [Fact]
    public void FindingsWithIdenticalContentAreEqual()
    {
        var left = Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(3, 5), "a", "b");
        var right = Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(3, 5), "a", "b");

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void FindingsDifferingOnlyInArgumentsAreNotEqual() =>
        Finding.Create(FindingCodes.ReferenceDangling, null, "Kunden")
            .ShouldNotBe(Finding.Create(FindingCodes.ReferenceDangling, null, "Artikel"));

    [Fact]
    public void FindingsDifferingOnlyInLocationAreNotEqual() =>
        Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(1, 1), "Kunden")
            .ShouldNotBe(Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(2, 1), "Kunden"));

    [Fact]
    public void FindingsDifferingOnlyInArgumentCountAreNotEqual() =>
        Finding.Create(FindingCodes.ReferenceDangling, null, "Kunden")
            .ShouldNotBe(Finding.Create(FindingCodes.ReferenceDangling, null, "Kunden", "extra"));

    [Fact]
    public void EqualFindingsCollapseInASet()
    {
        var set = new HashSet<Finding>
        {
            Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(3, 5), "a"),
            Finding.Create(FindingCodes.ReferenceDangling, new SourceLocation(3, 5), "a"),
        };

        set.Count.ShouldBe(1);
    }
}
