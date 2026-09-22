using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Content;

/// <summary>
/// Checking the one record in which a data file describes itself.
/// </summary>
public sealed class HeaderRowCheckTests
{
    private static readonly HeaderRowCheck Check = new();

    private static string Kunden(string range = "") => $"""
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              {range}
              <VariableLength>
                <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
                <VariableColumn><Name>Ort</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private const string SkipsFirst = "<Range><From>2</From></Range>";

    private static IReadOnlyList<Finding> Run(string range, string content) =>
        ContentCheckHarness.Run(Check, Kunden(range), "kunden.csv", content);

    [Fact]
    public void AHeaderInTheDeclaredOrderIsNotADefect() =>
        Run(SkipsFirst, "Code;Firma;Ort\r\nK1;Meier;Bonn\r\n").ShouldBeEmpty();

    [Fact]
    public void AHeaderInADifferentOrderIsReportedWithBothOrders()
    {
        var finding = Run(SkipsFirst, "Firma;Code;Ort\r\nMeier;K1;Bonn\r\n").ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.HeaderOrderDiffers);
        finding.Arguments[0].ShouldBe("Kunden");
        finding.Arguments[1].ShouldBe("Code, Firma, Ort");
        finding.Arguments[2].ShouldBe("Firma, Code, Ort");
        finding.Scope!.Kind.ShouldBe(FindingScopeKind.Table);
    }

    [Fact]
    public void AnExcludedRecordThatIsNotAHeaderIsNotReported() =>
        // A declaration may exclude a preamble. Accusing it of being a mis-ordered header would
        // be a false report about a file that is correct.
        Run(SkipsFirst, "Export vom 31.12.2024;;\r\nK1;Meier;Bonn\r\n").ShouldBeEmpty();

    [Fact]
    public void AnUndeclaredHeaderIsReportedAndNamesTheRowItWouldBecome()
    {
        var finding = Run(string.Empty, "Code;Firma;Ort\r\nK1;Meier;Bonn\r\n").ShouldHaveSingleItem();

        finding.Code.ShouldBe(FindingCodes.HeaderUndeclared);
        finding.Arguments[1].ShouldBe("Code, Firma, Ort");
    }

    [Fact]
    public void AnUndeclaredHeaderIsReportedWhateverOrderItIsWrittenIn() =>
        Run(string.Empty, "Ort;Code;Firma\r\nBonn;K1;Meier\r\n").ShouldHaveSingleItem()
            .Code.ShouldBe(FindingCodes.HeaderUndeclared);

    [Fact]
    public void AFirstRecordOfOrdinaryDataIsNotAHeader() =>
        Run(string.Empty, "K1;Meier;Bonn\r\nK2;Lang;Kiel\r\n").ShouldBeEmpty();

    [Fact]
    public void SurroundingWhitespaceDoesNotHideAHeader() =>
        Run(SkipsFirst, "Code; Firma ;Ort\r\nK1;Meier;Bonn\r\n").ShouldBeEmpty();

    [Fact]
    public void AnEncapsulatedHeaderIsStillRecognised() =>
        Run(SkipsFirst, "\"Firma\";\"Code\";\"Ort\"\r\nMeier;K1;Bonn\r\n").ShouldHaveSingleItem()
            .Code.ShouldBe(FindingCodes.HeaderOrderDiffers);

    [Fact]
    public void ARecordWithTheWrongNumberOfValuesIsNotJudgedAsAHeader() =>
        // The column-count finding says what is wrong here; guessing at a header on top of it
        // would be a second finding for one mistake.
        Run(SkipsFirst, "Code;Firma\r\nK1;Meier;Bonn\r\n").ShouldBeEmpty();

    [Fact]
    public void AnEmptyFileProducesNothing() =>
        Run(SkipsFirst, string.Empty).ShouldBeEmpty();
}
