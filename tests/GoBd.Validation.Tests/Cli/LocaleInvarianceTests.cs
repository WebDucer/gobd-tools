using System.Globalization;
using GoBd.Validation.Tests.Fixtures;
using GoBd.Validation.Tests.Parsing;

namespace GoBd.Validation.Tests.Cli;

/// <summary>
/// The guard behind design.md D5 and D8: a report is a function of the export and the chosen
/// language, never of the machine it ran on.
/// </summary>
public sealed class LocaleInvarianceTests
{
    private static readonly string[] Locales = ["de_DE.UTF-8", "en_US.UTF-8", "fr_FR.UTF-8", "C"];

    private static string RunUnder(string locale, string export, params string[] extra) =>
        CliHarness.RunWithEnvironment(
            new Dictionary<string, string?>
            {
                ["LANG"] = locale,
                ["LC_ALL"] = locale,
                ["LC_MESSAGES"] = locale,
                ["GOBD_LANG"] = null,
            },
            [export, "--language", "en", .. extra]).Output;

    [Fact]
    public void TextReportIsByteIdenticalAcrossOperatingSystemLocales()
    {
        var export = FixtureLibrary.FolderPath(FixtureLibrary.GoodExport);
        var baseline = RunUnder(Locales[0], export);

        foreach (var locale in Locales)
        {
            RunUnder(locale, export).ShouldBe(baseline, $"locale {locale} changed the report");
        }
    }

    [Fact]
    public void JsonReportIsByteIdenticalAcrossOperatingSystemLocales()
    {
        var export = FixtureLibrary.FolderPath(FixtureLibrary.GoodExport);
        var baseline = RunUnder(Locales[0], export, "--format", "json");

        foreach (var locale in Locales)
        {
            RunUnder(locale, export, "--format", "json").ShouldBe(baseline, $"locale {locale} changed the report");
        }
    }

    [Fact]
    public void ReportsCarryingNumbersAndDatesAreByteIdenticalAcrossLocales()
    {
        // Numbers and date masks are where a culture would show first: a German machine would
        // otherwise render 12,5 and 01.03.2019 where an English one renders 12.5 and 3/1/2019.
        var export = CliHarness.CreateExport(IndexXml.Document("""
              <Version>1.0</Version>
              <Media>
                <Name>Disk 1</Name>
                <Table>
                  <URL>sales.csv</URL>
                  <Name>Sales</Name>
                  <DecimalSymbol>,</DecimalSymbol>
                  <DigitGroupingSymbol>,</DigitGroupingSymbol>
                  <FixedLength>
                    <Length>8</Length>
                    <FixedColumn>
                      <Name>Betrag</Name>
                      <Date><Format>DD.MM</Format></Date>
                      <FixedRange><From>1</From><To>10</To></FixedRange>
                    </FixedColumn>
                    <FixedColumn>
                      <Name>Menge</Name>
                      <Numeric><Accuracy>2,5</Accuracy></Numeric>
                      <FixedRange><From>21</From><To>30</To></FixedRange>
                    </FixedColumn>
                  </FixedLength>
                </Table>
              </Media>
            """), includeDtd: true, ("sales.csv", "x\n"));
        try
        {
            var baseline = RunUnder(Locales[0], export);

            baseline.ShouldContain("GOBD3020");  // symbol collision, quoting ','
            baseline.ShouldContain("GOBD3022");  // unusable date mask, quoting 'DD.MM'
            baseline.ShouldContain("GOBD3019");  // '2,5' is not a usable integer
            baseline.ShouldContain("GOBD3017");  // positions 11 to 20 unclaimed

            foreach (var locale in Locales)
            {
                RunUnder(locale, export).ShouldBe(baseline, $"locale {locale} changed the report");
            }
        }
        finally
        {
            Directory.Delete(export, recursive: true);
        }
    }

    [Fact]
    public void ValuesQuotedFromIndexXmlAreReproducedVerbatim()
    {
        var export = CliHarness.CreateExport(IndexXml.Document("""
              <Version>1.0</Version>
              <Media>
                <Name>Disk 1</Name>
                <Table>
                  <URL>sales.csv</URL>
                  <Name>Sales</Name>
                  <DecimalSymbol>,</DecimalSymbol>
                  <DigitGroupingSymbol>,</DigitGroupingSymbol>
                  <VariableLength>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM</Format></Date></VariableColumn>
                  </VariableLength>
                </Table>
              </Media>
            """), includeDtd: true, ("sales.csv", "x\n"));
        try
        {
            var output = RunUnder("de_DE.UTF-8", export);

            // The declared symbol and mask must appear exactly as written, never reformatted.
            output.ShouldContain("','");
            output.ShouldContain("'DD.MM'");
        }
        finally
        {
            Directory.Delete(export, recursive: true);
        }
    }

    [Fact]
    public void TheProcessRunsInGlobalizationInvariantMode()
    {
        // Pins the deployment decision: invariant mode keeps the published binary free of a
        // libicu dependency, which a non-invariant process needs at start-up on Linux.
        //
        // Correctness does not rest on it -- the culture analyzers and explicit format providers
        // do -- so CI deliberately runs this suite once with InvariantGlobalization=false. Every
        // other test in this class must still pass there; only this one, which asserts the flag
        // itself, steps aside.
        AppContext.TryGetSwitch("System.Globalization.Invariant", out var invariant);
        Assert.SkipUnless(invariant, "Deliberately running with InvariantGlobalization=false.");

        Should.Throw<CultureNotFoundException>(() => CultureInfo.GetCultureInfo("de-DE"));
    }

    [Fact]
    public void ContentReportsAreByteIdenticalAcrossLocalesToo()
    {
        // Content findings quote values read out of a data file — amounts, dates, key values.
        // Those are precisely what a culture would reformat, so the guarantee that made the
        // structural report locale-proof has to be shown to extend to them.
        var export = CliHarness.CreateExport(IndexXml.Document("""
              <Version>1.0</Version>
              <Media>
                <Name>Disk 1</Name>
                <Table>
                  <URL>buchungen.csv</URL>
                  <Name>Buchungen</Name>
                  <VariableLength>
                    <VariablePrimaryKey><Name>Beleg</Name><AlphaNumeric/></VariablePrimaryKey>
                    <VariableColumn><Name>Datum</Name><Date><Format>DD.MM.YYYY</Format></Date></VariableColumn>
                    <VariableColumn><Name>Betrag</Name><Numeric><Accuracy>2</Accuracy></Numeric></VariableColumn>
                  </VariableLength>
                </Table>
              </Media>
            """), includeDtd: true, ("buchungen.csv", "B1;2024-12-31;1.234,5678\r\nB1;31.12.2024;12,50\r\n"));
        try
        {
            foreach (var format in new[] { "text", "json" })
            {
                var baseline = RunUnder(Locales[0], export, "--contents", "--format", format);

                baseline.ShouldContain("GOBD4001");   // the date does not match its mask
                baseline.ShouldContain("GOBD4002");   // four decimals where two are declared
                baseline.ShouldContain("GOBD5001");   // B1 is used twice as a primary key
                baseline.ShouldContain("1.234,5678"); // quoted as the file writes it

                foreach (var locale in Locales)
                {
                    RunUnder(locale, export, "--contents", "--format", format)
                        .ShouldBe(baseline, $"locale {locale} changed the {format} content report");
                }
            }
        }
        finally
        {
            Directory.Delete(export, recursive: true);
        }
    }
}
