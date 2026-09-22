using GoBd.Validation.Findings;
using GoBd.Validation.Model;
using GoBd.Validation.Parsing;
using GoBd.Validation.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Parsing;

public sealed class IndexXmlParserTests
{
    private static ParseOutcome ParseFixture()
    {
        using var stream = File.OpenRead(Path.Combine(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport), "index.xml"));
        return IndexXmlParser.Parse(stream);
    }

    [Fact]
    public void ConformantDocumentProducesNoGrammarFindings()
    {
        var outcome = ParseFixture();

        outcome.CanProceed.ShouldBeTrue();
        outcome.Findings.ShouldNotContain(finding => finding.Code == FindingCodes.GrammarViolation);
    }

    [Fact]
    public void ModelMirrorsTheDocument()
    {
        var dataSet = ParseFixture().DataSet.ShouldNotBeNull();

        dataSet.Version.Value.ShouldBe("1.0");
        dataSet.DataSupplier.ShouldNotBeNull().Name.Value.ShouldBe("Beispiel AG");
        dataSet.Media.Count.ShouldBe(1);
        dataSet.Tables.Select(table => table.Identity).ShouldBe(["Kunden", "Bestellungen"]);
    }

    [Fact]
    public void TableIdentityFallsBackToUrlWhenNoNameIsDeclared()
    {
        var outcome = IndexXmlParser.Parse(IndexXml.Stream(IndexXml.Document("""
              <Version>1.0</Version>
              <Media>
                <Name>D</Name>
                <Table>
                  <URL>region.csv</URL>
                  <VariableLength>
                    <VariableColumn><Name>Id</Name><AlphaNumeric/></VariableColumn>
                  </VariableLength>
                </Table>
              </Media>
            """)));

        outcome.DataSet.ShouldNotBeNull().Tables.Single().Identity.ShouldBe("region.csv");
    }

    [Fact]
    public void EveryNodeCarriesItsSourcePosition()
    {
        var dataSet = ParseFixture().DataSet.ShouldNotBeNull();
        var kunden = dataSet.Tables.First();
        var column = kunden.Format.ShouldNotBeNull().Columns[0];

        // Positions must increase as the walk descends, and be recorded on nested nodes -- they
        // cannot be recovered from the tree afterwards.
        dataSet.Location.Line.ShouldBe(3);
        kunden.Location.Line.ShouldBeGreaterThan(dataSet.Location.Line);
        column.Location.Line.ShouldBeGreaterThan(kunden.Location.Line);
        column.Name.Location.Line.ShouldBeGreaterThan(column.Location.Line);
        column.Location.Column.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ColumnsKeepDocumentOrderAndKeyFlags()
    {
        var format = ParseFixture().DataSet!.Tables.First().Format.ShouldNotBeNull();

        format.Columns.Select(column => column.Name.Value).ShouldBe(["Kunden-Code", "Firma"]);
        format.Columns[0].IsPrimaryKey.ShouldBeTrue();
        format.Columns[1].IsPrimaryKey.ShouldBeFalse();
        format.PrimaryKeys.Select(column => column.Name.Value).ShouldBe(["Kunden-Code"]);
    }

    [Fact]
    public void DeclaredScalarsArePreservedVerbatim()
    {
        var bestellungen = ParseFixture().DataSet!.Tables.Last();
        var format = bestellungen.Format.ShouldNotBeNull();
        var frachtkosten = format.Columns.Single(column => column.Name.Value == "Frachtkosten");

        // The model keeps declared text; interpreting it is a later check's job, so that an
        // unusable value becomes a finding rather than a parse failure.
        frachtkosten.Type.ShouldBeOfType<NumericType>().Accuracy.ShouldNotBeNull().Value.ShouldBe("2");
        format.Columns.Single(column => column.Name.Value == "Bestelldatum")
            .Type.ShouldBeOfType<DateType>().Format.ShouldNotBeNull().Value.ShouldBe("YYYYMMDD");
    }

    [Fact]
    public void ForeignKeysAreBound()
    {
        var format = ParseFixture().DataSet!.Tables.Last().Format.ShouldNotBeNull();
        var foreignKey = format.ForeignKeys.ShouldHaveSingleItem();

        foreignKey.Names.Select(name => name.Value).ShouldBe(["Kunden-Code"]);
        foreignKey.References.Value.ShouldBe("Kunden");
    }

    [Fact]
    public void CodepageIsBound() =>
        ParseFixture().DataSet!.Tables.First().Codepage.ShouldBe(Codepage.Utf8);
}
